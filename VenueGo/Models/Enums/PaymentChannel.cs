using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    public enum PaymentChannel : byte
    {
        /// <summary>
        /// 付款通道（Payments.PaymentChannel）— 具體「用什麼付的」。
        /// <para>
        /// 【組合限制】與 <see cref="PaymentMethod"/> 的允許組合如下，
        /// 其餘組合皆為無效資料，須於 Service 層驗證：
        /// </para>
        /// <list type="table">
        ///   <item>
        ///     <term><see cref="PaymentMethod.OnSite"/>（現場付款）</term>
        ///     <description>僅允許 <see cref="Counter"/>（現場櫃台）</description>
        ///   </item>
        ///   <item>
        ///     <term><see cref="PaymentMethod.Online"/>（線上付款）</term>
        ///     <description>僅允許 <see cref="CreditCard"/> 或 <see cref="LinePay"/></description>
        ///   </item>
        /// </list>
        /// <para>
        /// 期中只會用到 OnSite + Counter 這一組。
        /// </para>
        /// <para>
        /// 【勿混淆】本列舉是「付款通道」。
        /// 「預約從哪個介面建立」請用 <see cref="ReservationSource"/>，
        /// 兩者不可互用（LINE Pay 是付款通道，LINE 官方帳號才是預約來源）。
        /// </para>
        /// </summary>


        /// <summary>
        /// 現場櫃台：於場館櫃檯以現金或刷卡機收款。
        /// 【期中會使用】
        /// </summary>
        [Display(Name = "現場櫃台")]
        Counter = 0,

        /// <summary>
        /// 信用卡：線上信用卡金流。
        /// 【期末功能】
        /// </summary>
        [Display(Name = "信用卡")]
        CreditCard = 1,

        /// <summary>
        /// LINE Pay：LINE Pay 線上金流。
        /// 【期末功能】
        /// </summary>
        [Display(Name = "LINE Pay")]
        LinePay = 2
    }
}
