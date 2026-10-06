using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    /// <summary>
    /// 下單管道——這筆預約是誰在操作的時候建立的。
    /// <para>
    /// 【期末使用，目前尚無任何程式碼讀取這個值】前台會員自助預約功能還沒開工，
    /// 現在系統裡只有 <see cref="Counter"/> 這一種管道會真正被用到
    /// （後台代客的 <c>ReservationCreationService.WriteOnceAsync</c>）。
    /// 先把這個分類定義出來，等前台會員自助預約開始開發時，
    /// 用它搭配 <see cref="PaymentMethod"/> 分流計算付款期限。
    /// </para>
    /// </summary>
    public enum BookingChannel : byte
    {
        /// <summary>後台代客：員工在櫃檯幫會員建立預約。</summary>
        [Display(Name = "後台代客")]
        Counter = 0,

        /// <summary>前台會員自助預約：會員自己上線預約（期末功能）。</summary>
        [Display(Name = "會員自助預約")]
        Member = 1
    }
}
