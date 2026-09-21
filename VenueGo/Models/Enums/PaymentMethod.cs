using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    public enum PaymentMethod : byte
    {
        /// <summary>
        /// 付款方式（Payments.PaymentMethod）— 付款的「大分類」。
        /// <para>
        /// 與 <see cref="PaymentChannel"/>（具體用什麼付）搭配使用，
        /// 兩者的組合有限制，請見 <see cref="PaymentChannel"/> 的說明。
        /// </para>
        /// <para>
        /// 【設計備註】本欄位其實可由 <see cref="PaymentChannel"/> 推導
        /// （Counter → OnSite；CreditCard / LinePay → Online），
        /// 屬於刻意保留的冗餘欄位。因此務必在 Service 層集中驗證組合合法性，
        /// 避免出現「現場付款 + LINE Pay」這類矛盾資料。
        /// </para>
        /// </summary>


        /// <summary>
        /// 現場付款：會員到館於櫃檯付款。
        /// 【期中會使用】管理員代客建立預約時唯一會用到的方式。
        /// </summary>
        [Display(Name = "現場付款")]
        OnSite = 0,

        /// <summary>
        /// 線上付款：透過網路金流付款。
        /// 【期末功能】
        /// </summary>
        [Display(Name = "線上付款")]
        Online = 1
    }
}
