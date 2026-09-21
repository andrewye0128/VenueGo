using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace VenueGo.Extensions
{
    /// <summary>
    /// Session 的物件存取擴充方法。
    /// <para>
    /// ASP.NET Core 的 Session 只支援 string 與 byte[]，
    /// 要存自訂類別必須自己序列化。這裡包成兩個方法，
    /// 避免每個需要 Session 的地方都重複寫一次 JSON 轉換。
    /// </para>
    /// </summary>
    public static class SessionExtensions
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>將物件序列化後存入 Session。傳入 null 等同移除該筆資料。</summary>
        public static void SetObject<T>(this ISession session, string key, T? value)
        {
            if (value is null)
            {
                session.Remove(key);
                return;
            }
            session.SetString(key, JsonSerializer.Serialize(value, Options));
        }

        /// <summary>
        /// 從 Session 取出物件。不存在或內容無法反序列化時回傳 default，
        /// 不拋出例外，避免舊格式的 Session 資料讓整個頁面掛掉。
        /// </summary>
        public static T? GetObject<T>(this ISession session, string key)
        {
            var json = session.GetString(key);
            if (string.IsNullOrEmpty(json)) return default;

            try
            {
                return JsonSerializer.Deserialize<T>(json, Options);
            }
            catch (JsonException)
            {
                session.Remove(key);
                return default;
            }
        }
    }
}