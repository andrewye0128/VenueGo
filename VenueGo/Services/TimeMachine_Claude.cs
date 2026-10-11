using System.Globalization;
using System.Text.Json;

namespace VenueGo.Services
{
    /// <summary>
    /// <see cref="ITimeMachine"/> 的實作。必須註冊成 Singleton，全站共用同一個偏移量。
    /// <para>
    /// 【為什麼不直接改電腦時鐘】
    /// 1. 改電腦時鐘會影響整台電腦，也會讓 TimeService 的自動校時把時間「校回來」。
    /// 2. 時間前後亂跳，出入場紀錄的時間會比前一筆還早，依時間排序的查詢就會出錯。
    /// 時光機只動網站內部的時間，而且隨時可以回到現在。
    /// </para>
    /// <para>
    /// 【操作】網站執行中打開 /DevTools/Time（後台頁面右下角的 ⏰ 也能開），
    /// 或在提示條上按「調整」。
    /// </para>
    /// <para>
    /// 【重新啟動會保持上次的狀態】每次調整都會存到這個檔案：
    /// <c>%LOCALAPPDATA%\VenueGo\time-machine.json</c>（只在自己的電腦，不會進 git）。
    /// 以前偏移量只放在記憶體，Hot Reload 自動重啟、停止再開始偵錯，時光機就會悄悄回到現在，
    /// 提示條也跟著不見。想換存檔位置，在 appsettings.Local.json 寫 <c>"TimeMachine": { "StateFile": "路徑" }</c>；
    /// 寫空字串 <c>""</c> 就是不存檔（單元測試就是這樣用）。
    /// </para>
    /// <para>
    /// 【啟動時就出發】在 appsettings.Local.json 寫：
    /// <code>"TimeMachine": { "TravelTo": "2026-10-30 16:45" }</code>
    /// 只在「第一次啟動」或「這個值改過」的時候生效；之後重新啟動都以上次的狀態為準，
    /// 所以按過「回到現在」，重新啟動也不會又跳回去。
    /// </para>
    /// </summary>
    public sealed class TimeMachine : ITimeMachine
    {
        /// <summary>appsettings 裡「啟動時跳到哪」的設定鍵。</summary>
        public const string TravelToKey = "TimeMachine:TravelTo";

        /// <summary>appsettings 裡「存檔位置」的設定鍵。沒寫用預設位置，空字串代表不存檔。</summary>
        public const string StateFileKey = "TimeMachine:StateFile";

        /// <summary>設定檔可以接受的時間寫法（都視為台北時間）。</summary>
        private static readonly string[] AcceptedFormats =
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm",
            "yyyy-MM-dd",
            "yyyy/M/d HH:mm:ss",
            "yyyy/M/d HH:mm",
            "yyyy/M/d",
        };

        private readonly TimeProvider _timeProvider;
        private readonly ILogger<TimeMachine> _logger;

        /// <summary>存檔的完整路徑；null 代表不存檔。</summary>
        private readonly string? _stateFile;

        /// <summary>這次啟動時設定檔裡的 TravelTo（去掉前後空白；沒寫是 null）。存檔時一起記下來。</summary>
        private readonly string? _configuredTravelTo;

        /// <summary>改偏移量＋存檔要一起完成，避免兩個請求同時調整時，檔案存到比較舊的那個值。</summary>
        private readonly Lock _writeLock = new();

        /// <summary>偏移量（Ticks）。用 Interlocked 讀寫，多個請求同時讀也安全。</summary>
        private long _offsetTicks;

        /// <summary>最後一次調整的記號，見 <see cref="Stamp"/>。</summary>
        private long _stamp;

        public TimeMachine(
            IHostEnvironment environment,
            IConfiguration configuration,
            TimeProvider timeProvider,
            ILogger<TimeMachine> logger)
        {
            _timeProvider = timeProvider;
            _logger = logger;
            IsAvailable = environment.IsDevelopment();

            string? travelTo = configuration[TravelToKey]?.Trim();
            _configuredTravelTo = string.IsNullOrEmpty(travelTo) ? null : travelTo;

            if (!IsAvailable)
            {
                // 正式環境就算設定檔寫了也不理會，只記一筆警告提醒有人忘了拿掉；存檔也完全不碰
                if (_configuredTravelTo != null)
                    _logger.LogWarning("設定了 {Key}，但目前不是開發環境，時光機不會啟動", TravelToKey);
                return;
            }

            _stateFile = ResolveStateFile(configuration[StateFileKey]);

            if (TryRestore()) return;
            if (_configuredTravelTo == null) return;

            if (!TryParseTaipeiTime(_configuredTravelTo, out DateTime target))
            {
                _logger.LogWarning("{Key} 的值「{Value}」看不懂，時光機不會啟動。範例：2026-10-30 16:45", TravelToKey, _configuredTravelTo);
                return;
            }

            TravelTo(target);
        }

        public bool IsAvailable { get; }

        public TimeSpan TravelOffset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

        public bool IsTraveling => TravelOffset != TimeSpan.Zero;

        public long Stamp => Interlocked.Read(ref _stamp);

        public void TravelTo(DateTime taipeiTime)
        {
            EnsureAvailable();
            DateTime realNow = _timeProvider.GetLocalNow().DateTime;
            SetOffset(taipeiTime - realNow);
        }

        public void TravelBy(TimeSpan delta)
        {
            EnsureAvailable();
            lock (_writeLock)
            {
                // 讀和寫放在同一把鎖裡：兩個 +1 天同時進來，結果要是 +2 天，不能互相蓋掉
                SetOffset(TravelOffset + delta);
            }
        }

        public void Reset()
        {
            // 回到現在不需要檢查環境：正式環境本來就是 0，重設一次也無妨
            SetOffset(TimeSpan.Zero);
        }

        /// <summary>
        /// 把設定檔或網址上的文字轉成台北時間。看不懂就回 false。
        /// 公開出來是因為 /DevTools/Time 也要用同一套規則。
        /// </summary>
        public static bool TryParseTaipeiTime(string? text, out DateTime taipeiTime)
            => DateTime.TryParseExact(text?.Trim(), AcceptedFormats, CultureInfo.InvariantCulture,
                                      DateTimeStyles.None, out taipeiTime);

        private void SetOffset(TimeSpan offset)
        {
            lock (_writeLock)
            {
                Interlocked.Exchange(ref _offsetTicks, offset.Ticks);

                // 記號用現在的 UTC Ticks；同一個 Tick 內調整兩次也要不一樣，所以至少 +1
                long next = Math.Max(_timeProvider.GetUtcNow().UtcTicks, Stamp + 1);
                Interlocked.Exchange(ref _stamp, next);

                Save();
            }

            if (offset == TimeSpan.Zero)
                _logger.LogInformation("時光機：回到真實時間");
            else
                _logger.LogWarning("時光機：網站時間偏移 {Offset}（正數是未來、負數是過去）", offset);
        }

        private void EnsureAvailable()
        {
            if (!IsAvailable)
                throw new InvalidOperationException("時光機只能在開發環境使用。");
        }

        // ════════════════════════════════════════════════════════
        //  存檔：讓網站重新啟動後還記得
        // ════════════════════════════════════════════════════════

        /// <summary>存檔的內容。TravelTo 記的是存檔當下設定檔裡的值，用來判斷「設定檔有沒有改過」。</summary>
        private sealed record SavedState(long OffsetTicks, long Stamp, string? TravelTo);

        private static string? ResolveStateFile(string? configured)
        {
            if (configured != null)
                return configured.Trim().Length == 0 ? null : configured.Trim();

            string baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(baseFolder)) baseFolder = Path.GetTempPath();
            return Path.Combine(baseFolder, "VenueGo", "time-machine.json");
        }

        /// <summary>讀上次的狀態。沒有存檔、設定檔的 TravelTo 改過、或檔案壞掉，都回 false。</summary>
        private bool TryRestore()
        {
            if (_stateFile == null || !File.Exists(_stateFile)) return false;

            // try/catch 的決定：讀檔失敗（檔案被鎖住、內容壞掉）只是「這次不還原」，
            // 不應該讓整個網站啟動失敗。所以只記警告，接著照設定檔走。
            try
            {
                var saved = JsonSerializer.Deserialize<SavedState>(File.ReadAllText(_stateFile));
                if (saved == null) return false;

                if (saved.TravelTo != _configuredTravelTo)
                {
                    _logger.LogInformation("時光機：{Key} 改過了，改用設定檔的值，不還原上次的狀態", TravelToKey);
                    return false;
                }

                Interlocked.Exchange(ref _offsetTicks, saved.OffsetTicks);
                Interlocked.Exchange(ref _stamp, saved.Stamp);

                if (saved.OffsetTicks != 0)
                    _logger.LogWarning("時光機：還原上次的狀態，網站時間偏移 {Offset}", TravelOffset);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogWarning(ex, "時光機：讀不到上次的狀態（{File}），這次不還原", _stateFile);
                return false;
            }
        }

        /// <summary>存檔。呼叫端已經拿著 _writeLock。</summary>
        private void Save()
        {
            if (_stateFile == null) return;

            // try/catch 的決定：存檔失敗只影響「重新啟動後還記不記得」，
            // 這次的調整已經生效了，不應該讓使用者的操作失敗。所以只記警告。
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_stateFile)!);

                // 先寫到暫存檔再換過去：寫到一半當機的話，舊檔還是完整的
                string temp = _stateFile + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(new SavedState(_offsetTicks, _stamp, _configuredTravelTo)));
                File.Move(temp, _stateFile, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "時光機：存不了檔（{File}），重新啟動後會回到現在", _stateFile);
            }
        }
    }
}
