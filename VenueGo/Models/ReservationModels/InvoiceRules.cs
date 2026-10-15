using System.Text.RegularExpressions;

namespace VenueGo.Models.ReservationModels
{
    /// <summary>
    /// 發票相關的共用規則：後台代客建立、前台會員預約都用這一份。
    /// <para>
    /// 【為何獨立出來】載具格式原本是 ReservationCreationService 的私有欄位，
    /// 前台會員預約也要檢查同樣的格式。放在這裡大家共用，格式只寫一次，
    /// 不會發生「後台改了、前台忘了改」的情況。
    /// </para>
    /// </summary>
    public static class InvoiceRules
    {
        /// <summary>
        /// 手機條碼載具的格式：斜線加 7 碼大寫英數字或 + - . 三個符號，例如 /AB12345。
        /// <para>前端 src/constants/payment.js 的 CARRIER_NO_PATTERN 要跟這裡一致。</para>
        /// </summary>
        public static readonly Regex CarrierNoPattern =
            new(@"^/[0-9A-Z+\-.]{7}$", RegexOptions.Compiled);

        /// <summary>
        /// 載具號碼格式是否正確。
        /// <para>只檢查格式，不會幫忙去空白、轉大寫；需要的話請先呼叫 <see cref="NormalizeCarrierNo"/>。</para>
        /// </summary>
        public static bool IsValidCarrierNo(string? carrierNo)
            => !string.IsNullOrEmpty(carrierNo) && CarrierNoPattern.IsMatch(carrierNo);

        /// <summary>
        /// 整理載具號碼：去掉前後空白、轉成大寫。空白或 null 時回傳 null。
        /// <para>前台客人可能打小寫，整理後再檢查格式、寫進資料庫。</para>
        /// </summary>
        public static string? NormalizeCarrierNo(string? carrierNo)
        {
            var value = carrierNo?.Trim().ToUpperInvariant();
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
