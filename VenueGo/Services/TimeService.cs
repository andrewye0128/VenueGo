using System.Text.Json;
using System.Text.Json.Serialization;

namespace VenueGo.Services
{
    public class TimeService(IHttpClientFactory httpClientFactory) : ITimeService
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public async Task<DateTime> GetCurrentTimeAsync(string timeZone = "Asia/Taipei")
        {
            var client = _httpClientFactory.CreateClient();
            string url = $"https://timeapi.io{timeZone}";

            try
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();

                // 使用預設的不區分大小寫設定解析 JSON
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<TimeApiDto>(jsonString, options);

                if (result == null) throw new Exception("無法解析時間 API 回傳的資料。");

                // 解析 ISO 8601 時間字串 (例如: "2026-09-21T16:50:25.123456")
                DateTime parsedTime = DateTime.Parse(result.DateTime);

                // 完美符合 datetime2(0)：無條件捨去毫秒與微秒
                return new DateTime(
                    parsedTime.Year,
                    parsedTime.Month,
                    parsedTime.Day,
                    parsedTime.Hour,
                    parsedTime.Minute,
                    parsedTime.Second,
                    DateTimeKind.Unspecified // SQL Server datetime2 通常不帶時區資訊
                );
            }
            catch (Exception ex)
            {
                // 這裡可以加入您的 Log 機制（例如 ILogger）
                // 備援方案：若外部 API 壞掉，至少回傳伺服器本地時間，確保系統不崩潰
                return DateTime.Now;
            }
        }

        // 僅供 Service 內部使用的 DTO
        private class TimeApiDto
        {
            public string DateTime { get; set; } = string.Empty;
        }
    }
}
