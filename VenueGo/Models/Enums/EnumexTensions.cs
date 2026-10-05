using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace VenueGo.Models.Enums
{
    public static class EnumExtensions
    {
        /// <summary>
        /// 取得列舉值上 <see cref="DisplayAttribute"/> 所設定的顯示名稱。
        /// 若該值未加上 Display 標註，則退回回傳列舉值的英文名稱，
        /// 因此畫面上若出現英文即代表該列舉漏加標註，便於排查。
        /// </summary>
        /// <param name="value">任何列舉值，例如 PaymentStatus.Paid。</param>
        /// <returns>顯示名稱，例如「已付款」。</returns>
        /// <example>
        /// <code>
        /// // Razor View
        /// &lt;td&gt;@item.ReservationStatus.GetDisplayName()&lt;/td&gt;
        ///
        /// // ViewModel（期末給 Vue 前端直接使用，前端不必再維護一份對照表）
        /// public string StatusText =&gt; Status.GetDisplayName();
        /// </code>
        /// </example>
        public static string GetDisplayName(this Enum value)
        {
            if (value is null)
            {
                return string.Empty;
            }

            // 步驟一：取得這個值所屬的列舉型別，例如 PaymentStatus
            // 步驟二：列舉的每個成員編譯後皆為 static field，依名稱找出該成員
            // 步驟三：從該成員上取出 [Display(...)] 標註
            // 步驟四：讀取標註的 Name 屬性
            // 任一步驟取不到時，?. 會讓整串結果為 null，不會拋出 NullReferenceException
            return value.GetType()
                        .GetField(value.ToString())
                        ?.GetCustomAttribute<DisplayAttribute>()
                        ?.Name
                   ?? value.ToString();
        }

        /// <summary>
        /// 這個預約狀態是否仍屬於「有效」——時段仍被佔用，可以繼續做後續操作
        /// （標記付款、取消、作廢）。只有待確認、已確認這兩種狀態算有效。
        /// <para>
        /// 抽成共用方法，是因為預約詳細頁與預約列表頁都要用到同一條規則，
        /// 複製兩份的話，以後規則變了很容易改一邊忘記改另一邊。
        /// </para>
        /// </summary>
        public static bool IsActive(this ReservationStatus status) =>
            status is ReservationStatus.Pending or ReservationStatus.Confirmed;
    }
}
