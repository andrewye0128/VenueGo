using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace VenueGo.Services
{
    /// <summary>
    /// ITimeService 的實作。
    /// ⚠️ 必須註冊為 Singleton——它要「記住」偏移量。
    ///    註冊成 Scoped 的話每個請求都是新物件，什麼都記不住。
    /// </summary>
    public class TimeService : ITimeService
    {
        /// <summary>給 AddHttpClient 用的名字，Program.cs 要用同一個字串。</summary>
        public const string HttpClientName = "TimeApi";

        /// <summary>
        /// 預設的時間 API 端點。已實際呼叫驗證過，回傳長這樣：
        ///   {"year":2026,"month":9,"day":21,"hour":11,"minute":7,"seconds":15,
        ///    "milliSeconds":272,"dateTime":"2026-09-21T11:07:15.2721376",
        ///    "date":"09/21/2026","time":"11:07","timeZone":"Asia/Taipei",
        ///    "dayOfWeek":"Monday","dstActive":false}
        /// 我們只取 dateTime 這個欄位。
        ///
        /// 想換來源的話寫進 appsettings.json 的 TimeApi:Url 就好，不必改程式碼；
        /// 但換掉的 API 回傳欄位也要叫 dateTime，否則 TimeApiDto 要跟著改。
        /// </summary>
        private const string DefaultUrl =
            "https://timeapi.io/api/time/current/zone?timeZone=Asia/Taipei";

        /// <summary>多久校時一次。時鐘漂移以天為單位累積，半小時綽綽有餘。</summary>
        public static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(30);

        /// <summary>
        /// 死區：量到的偏移量絕對值小於這個值，就當作 0。
        ///
        /// 為什麼要有死區？資料庫欄位是 datetime2(0)，精度只到秒，
        /// 小於一秒的修正存進去根本看不出來。但每次校時量到的值都會隨網路狀況抖動，
        /// 照單全收只會讓 Now 在兩次校時之間無意義地跳來跳去。
        ///
        /// ⚠️ 副作用：真實偏移量剛好卡在 1 秒附近時，會在「0」和「1 秒」之間來回跳。
        ///    要避免的話得做遲滯（進入死區的門檻比離開的低），
        ///    但對 datetime2(0) 來說差別看不出來，不值得為它增加複雜度。
        /// </summary>
        public static readonly TimeSpan OffsetDeadBand = TimeSpan.FromSeconds(1);

        /// <summary>
        /// 往返時間超過這個值，這次測量就整筆不採信。
        ///
        /// 中點補償的假設是「去程和回程各花一半」，實際不會剛好一半，
        /// 所以這次測量的誤差上限就是「往返時間的一半」。
        /// 往返一秒 → 誤差可能到半秒；往返十秒 → 誤差可能到五秒，
        /// 那量到的偏移量基本上全是網路雜訊，拿來校時只會越校越歪。
        /// 寧可沿用上一次的值。NTP 也是這樣篩的。
        ///
        /// 為什麼是 2 秒＝死區的兩倍：誤差上限是往返的一半，
        /// 所以往返 2 秒時誤差上限剛好等於死區 1 秒。再久下去，
        /// 「超過死區」就可能純粹是雜訊造成的，篩不出真訊號。
        ///
        /// ⚠️ 這個值必須小於 HttpClient 的 Timeout（Program.cs 設 5 秒），
        ///    否則請求會先逾時，這道篩選永遠不會被執行到。
        /// </summary>
        public static readonly TimeSpan MaxRoundTrip = TimeSpan.FromSeconds(2);

        private static readonly JsonSerializerOptions JsonOptions =
            new() { PropertyNameCaseInsensitive = true };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TimeService> _logger;
        private readonly string _url;

        /// <summary>
        /// 同一時間只讓一次校時進行。Singleton 會被多執行緒共用，
        /// 沒有這道門的話背景服務跟手動觸發可能同時打 API。
        /// </summary>
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>
        /// 偏移量存成 long（ticks）而不是 TimeSpan，是為了能用 Interlocked
        /// 做不可分割的讀寫。多執行緒同時讀寫一個結構有可能讀到寫到一半的值。
        /// </summary>
        private long _offsetTicks;

        /// <summary>
        /// 上次校時成功的時間，同樣存成 long（ticks）。
        /// ⚠️ 原本寫成 DateTime?，那是 struct，多執行緒同時讀寫可能讀到寫到一半的值。
        ///    影響只有這個診斷欄位（偏移量本來就有 Interlocked 保護），
        ///    但既然旁邊那個都做了，這個沒道理不做。0 代表從未成功過。
        /// </summary>
        private long _lastSyncedAtTicks;

        public TimeService(
            IHttpClientFactory httpClientFactory,
            ILogger<TimeService> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            // 網址寫進設定檔，之後要修正不必改程式碼重新編譯
            _url = configuration["TimeApi:Url"] ?? DefaultUrl;
        }

        public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

        public DateTime? LastSyncedAt
        {
            get
            {
                long ticks = Interlocked.Read(ref _lastSyncedAtTicks);
                return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Unspecified);
            }
        }

        public DateTime Now => TruncateToSecond(DateTime.Now + Offset);

        public DateTime Today => Now.Date;

        public async Task SyncAsync(CancellationToken cancellationToken = default)
        {
            // WaitAsync(0) = 「拿得到就拿，拿不到就算了」，不會排隊等待。
            // 已經有人在校時的話這次就跳過，不必兩個人同時打同一個 API。
            if (!await _gate.WaitAsync(0, cancellationToken)) return;

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);

                // ── 為什麼要記「送出前」與「收到後」兩個時間 ──────────
                //  你問對方「現在幾點」，對方回答的那一刻到你收到的那一刻，
                //  時間又過去了一點。如果直接拿「收到時的本地時間」去比，
                //  會把整趟網路往返的時間都算成誤差。
                //  用一來一回的「中點」當基準，等於假設去程和回程各花一半，
                //  剩下的誤差只有半趟。這也是 NTP 的核心想法。
                DateTime localBefore = DateTime.Now;

                using var response = await client.GetAsync(_url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var dto = await response.Content
                                        .ReadFromJsonAsync<TimeApiDto>(JsonOptions, cancellationToken);

                DateTime localAfter = DateTime.Now;

                if (string.IsNullOrWhiteSpace(dto?.DateTime))
                    throw new InvalidOperationException("時間 API 的回應裡沒有 dateTime 欄位。");

                // 明確指定不變文化，不要跟著機器的地區設定跑
                DateTime apiTime = DateTime.Parse(
                    dto!.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None);

                TimeSpan roundTrip = localAfter - localBefore;
                DateTime localMid   = localBefore + roundTrip / 2;
                TimeSpan measured   = apiTime - localMid;

                // ── 篩選一：往返太久，這次量到的數字不可信 ──
                if (roundTrip > MaxRoundTrip)
                {
                    _logger.LogWarning(
                        "校時放棄：往返 {RoundTrip} 毫秒超過上限，量到的偏移量 {Measured} 不可信，沿用 {Offset}",
                        roundTrip.TotalMilliseconds, measured, Offset);
                    return;   // finally 還是會 Release，不會卡住下一次
                }

                // ── 篩選二：死區。小於一秒的偏移對 datetime2(0) 沒有意義 ──
                TimeSpan applied = measured.Duration() < OffsetDeadBand
                                   ? TimeSpan.Zero
                                   : measured;

                Interlocked.Exchange(ref _offsetTicks, applied.Ticks);
                Interlocked.Exchange(ref _lastSyncedAtTicks, localAfter.Ticks);

                if (applied == TimeSpan.Zero && measured != TimeSpan.Zero)
                {
                    _logger.LogInformation(
                        "校時成功：量到偏移量 {Measured}，小於死區 {DeadBand}，視為 0（往返 {RoundTrip} 毫秒）",
                        measured, OffsetDeadBand, roundTrip.TotalMilliseconds);
                }
                else
                {
                    _logger.LogInformation(
                        "校時成功：偏移量 {Offset}（往返 {RoundTrip} 毫秒）",
                        Offset, roundTrip.TotalMilliseconds);
                }
            }
            catch (Exception ex)
            {
                // ⚠️ 失敗時「沿用上一次的偏移量」，不要歸零。
                //    上次算出來的偏移量仍然比完全沒有偏移量準確。
                //    而且一定要記 log——之前那版把例外吞掉，
                //    結果網址寫錯（少一個斜線）整整沒人發現。
                _logger.LogWarning(ex,
                    "校時失敗，沿用現有偏移量 {Offset}（上次成功校時：{LastSyncedAt}）",
                    Offset, LastSyncedAt);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>捨去毫秒與微秒，符合資料庫的 datetime2(0)。</summary>
        private static DateTime TruncateToSecond(DateTime value)
            => new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Unspecified);

        /// <summary>只在這個類別內部用的 DTO。</summary>
        private sealed class TimeApiDto
        {
            public string? DateTime { get; set; }
        }
    }
}
