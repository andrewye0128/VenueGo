using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    public enum InvoiceType : byte
    {
        /// <summary>
        /// 發票類型（Orders.InvoiceType）。
        /// <para>
        /// 【驗證規則】本列舉決定 Orders.CarrierNo 是否必填：
        /// </para>
        /// <list type="bullet">
        ///   <item><see cref="Paper"/>：CarrierNo 必須為 NULL。
        ///   UI 上選此項時要 disable 載具欄位並清空其值，
        ///   不可把使用者先前輸入的內容一併送出。</item>
        ///   <item><see cref="MobileBarcode"/>：CarrierNo 必填，
        ///   格式為斜線加 7 碼大寫英數字（例：/AB12345）。</item>
        /// </list>
        /// <para>
        /// 會員若已於 Users.CarrierNo 留存載具，新增預約時可自動帶入。
        /// </para>
        /// </summary>


        /// <summary>
        /// 現場索取發票：由櫃檯列印紙本交付會員，不使用載具。
        /// 【期中會使用】
        /// </summary>
        [Display(Name = "現場索取發票")]
        Paper = 0,

        /// <summary>
        /// 手機條碼載具：發票存入會員的手機條碼，需填寫 CarrierNo。
        /// 【期中會使用】
        /// </summary>
        [Display(Name = "手機條碼載具")]
        MobileBarcode = 1
    }
}
