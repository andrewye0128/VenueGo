using System.Globalization;
using System.Text.Json;
using VenueGo.Dtos.WeatherDtos;

namespace VenueGo.Services.Weather
{
    /// <summary>
    /// 把中央氣象署開放資料的 JSON 轉成 TopBar 要的資料（10/18）。純計算、不碰網路，方便用真實回應寫單元測試。
    /// <para>
    /// 欄位名稱照氣象署 2024 年改版後的格式（records.Station[].WeatherElement.AirTemperature、
    /// records.Locations[].Location[].WeatherElement[].Time[].ElementValue[].MaxTemperature⋯）。
    /// 找欄位一律不分大小寫；找不到的值當成沒有（null），不丟例外：氣象署改了一個欄位，只會少顯示那一項，網站不會壞。
    /// </para>
    /// </summary>
    public static class CwaWeatherParser
    {
        /// <summary>氣象署用 -99、-999 之類的值代表「沒有資料」。</summary>
        private const double MissingBelow = -90;

        // ════════════════════════════════════════════════════════
        //  現在天氣（O-A0003-001 局屬氣象站）
        // ════════════════════════════════════════════════════════

        /// <summary>取 stationId 那一站；找不到就取第一站。溫度拿不到就回 null。</summary>
        public static CurrentWeatherDto? ParseObservation(JsonElement root, string stationId)
        {
            if (Get(Get(root, "records"), "Station") is not { ValueKind: JsonValueKind.Array } stations)
                return null;

            JsonElement? station = null;
            foreach (var s in stations.EnumerateArray())
            {
                station ??= s;
                if (Text(Get(s, "StationId")) == stationId)
                {
                    station = s;
                    break;
                }
            }
            if (station is not JsonElement st) return null;

            var element = Get(st, "WeatherElement");
            double? temperature = Number(Get(element, "AirTemperature"));
            if (temperature is not double t) return null;

            string? weather = Text(Get(element, "Weather"));
            if (weather is not null && double.TryParse(weather, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) && w < MissingBelow)
                weather = null;   // 有些站沒有天氣現象，會給 "-99"

            DateTimeOffset? observedAt = Time(Get(Get(st, "ObsTime"), "DateTime"));
            if (observedAt is not DateTimeOffset at) return null;

            return new CurrentWeatherDto(Math.Round(t, 1), weather, at);
        }

        // ════════════════════════════════════════════════════════
        //  三天預報（F-D0047-063 鄉鎮未來一週，12 小時一段）
        // ════════════════════════════════════════════════════════

        /// <summary>一段預報時間裡的值。</summary>
        private sealed class Period(DateTimeOffset start, DateTimeOffset end)
        {
            public DateTimeOffset Start { get; } = start;
            public DateTimeOffset End { get; } = end;
            public int? Max { get; set; }
            public int? Min { get; set; }
            public int? Temperature { get; set; }
            public string? Weather { get; set; }
        }

        /// <summary>
        /// 回傳 today 起算的三天：今天、明天、後天。
        /// <list type="bullet">
        ///   <item>每一段預報歸到它「開始」的那一天（18:00～隔天 06:00 那一段算在當天晚上）。
        ///         今天另外加上「已經開始、還沒結束」的那一段（例如凌晨兩點時，昨晚 18:00 開始的那段）。</item>
        ///   <item>最高溫取當天各段的最高、最低溫取各段的最低；資料只有「溫度」（逐 3 小時的資料集）時，用溫度的最高、最低代替。</item>
        ///   <item>天氣取白天那一段（06:00 開始），沒有白天那段（例如今天晚上才看）就取第一段。</item>
        /// </list>
        /// </summary>
        public static IReadOnlyList<DailyForecastDto> ParseForecast(JsonElement root, string locationName, DateTimeOffset now)
        {
            var periods = ReadPeriods(root, locationName, now.Offset);
            string[] labels = ["今天", "明天", "後天"];
            DateOnly today = DateOnly.FromDateTime(now.DateTime);

            var days = new List<DailyForecastDto>(labels.Length);
            for (int i = 0; i < labels.Length; i++)
            {
                DateOnly date = today.AddDays(i);
                var mine = periods.Where(p => DateOnly.FromDateTime(p.Start.DateTime) == date).ToList();
                if (i == 0)
                {
                    // 今天：再加上開始在今天以前、現在還沒結束的那一段
                    var ongoing = periods.FirstOrDefault(p => DateOnly.FromDateTime(p.Start.DateTime) < date && p.End > now);
                    if (ongoing is not null) mine.Insert(0, ongoing);
                }
                if (mine.Count == 0) continue;

                int? max = Max(mine.Select(p => p.Max)) ?? Max(mine.Select(p => p.Temperature));
                int? min = Min(mine.Select(p => p.Min)) ?? Min(mine.Select(p => p.Temperature));
                string? weather = (mine.FirstOrDefault(p => p.Start.Hour == 6) ?? mine[0]).Weather;

                days.Add(new DailyForecastDto(date, labels[i], min, max, weather));
            }
            return days;
        }

        private static List<Period> ReadPeriods(JsonElement root, string locationName, TimeSpan offset)
        {
            var byStart = new Dictionary<DateTimeOffset, Period>();

            if (Get(Get(root, "records"), "Locations") is not { ValueKind: JsonValueKind.Array } groups)
                return [];

            JsonElement? location = null;
            foreach (var g in groups.EnumerateArray())
            {
                if (Get(g, "Location") is not { ValueKind: JsonValueKind.Array } list) continue;
                foreach (var l in list.EnumerateArray())
                {
                    location ??= l;
                    if (Text(Get(l, "LocationName")) == locationName)
                    {
                        location = l;
                        break;
                    }
                }
            }
            if (location is not JsonElement loc) return [];
            if (Get(loc, "WeatherElement") is not { ValueKind: JsonValueKind.Array } elements) return [];

            // 不看 ElementName（中文名稱），直接看 ElementValue 裡有哪些欄位：名稱改了也找得到
            foreach (var element in elements.EnumerateArray())
            {
                if (Get(element, "Time") is not { ValueKind: JsonValueKind.Array } times) continue;
                foreach (var time in times.EnumerateArray())
                {
                    // 逐 3 小時的「溫度」只有 DataTime（一個時間點，當成 3 小時一段），12 小時一段的有 StartTime、EndTime
                    DateTimeOffset? start = Time(Get(time, "StartTime")) ?? Time(Get(time, "DataTime"));
                    if (start is not DateTimeOffset s) continue;
                    s = s.ToOffset(offset);
                    DateTimeOffset end = (Time(Get(time, "EndTime")) ?? s.AddHours(3)).ToOffset(offset);
                    if (!byStart.TryGetValue(s, out var period))
                        byStart[s] = period = new Period(s, end);

                    if (Get(time, "ElementValue") is not { ValueKind: JsonValueKind.Array } values) continue;
                    foreach (var v in values.EnumerateArray())
                    {
                        period.Max ??= Integer(Get(v, "MaxTemperature"));
                        period.Min ??= Integer(Get(v, "MinTemperature"));
                        period.Temperature ??= Integer(Get(v, "Temperature"));
                        period.Weather ??= NonEmpty(Text(Get(v, "Weather")));
                    }
                }
            }
            return byStart.Values.OrderBy(p => p.Start).ToList();
        }

        // ════════════════════════════════════════════════════════
        //  讀 JSON 的小工具：不分大小寫、型別不對就當成沒有
        // ════════════════════════════════════════════════════════

        private static JsonElement? Get(JsonElement? parent, string name)
        {
            if (parent is not { ValueKind: JsonValueKind.Object } obj) return null;
            foreach (var p in obj.EnumerateObject())
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    return p.Value;
            return null;
        }

        private static string? Text(JsonElement? e) => e switch
        {
            { ValueKind: JsonValueKind.String } s => s.GetString(),
            { ValueKind: JsonValueKind.Number } n => n.GetRawText(),
            _ => null
        };

        private static string? NonEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>數字或數字字串都接受（氣象署的預報值是字串 "25"，觀測值是數字 25.3）。</summary>
        private static double? Number(JsonElement? e)
        {
            string? raw = Text(e);
            if (raw is null || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return null;
            return d < MissingBelow ? null : d;
        }

        private static int? Integer(JsonElement? e) => Number(e) is double d ? (int)Math.Round(d) : null;

        private static DateTimeOffset? Time(JsonElement? e)
            => Text(e) is string s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;

        /// <summary>有值的才算；一個值都沒有就是 null。</summary>
        private static int? Max(IEnumerable<int?> xs)
        {
            var values = xs.OfType<int>().ToList();
            return values.Count == 0 ? null : values.Max();
        }

        private static int? Min(IEnumerable<int?> xs)
        {
            var values = xs.OfType<int>().ToList();
            return values.Count == 0 ? null : values.Min();
        }
    }
}
