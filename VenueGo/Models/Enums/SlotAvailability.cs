using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    /// <summary>
    /// 單一時段的可用狀態。
    /// <para>
    /// 這個列舉不對應任何資料庫欄位，它是 TimeSlotService 計算後的結果。
    /// 之所以要區分這麼多種「不可選」，是因為櫃檯必須能回答會員
    /// 「為什麼這個時段訂不到」——「已被預約」與「場地維護」的處理方式完全不同：
    /// 前者可以建議改訂其他場地，後者整個場地當天都不能用。
    /// </para>
    /// </summary>
    public enum SlotAvailability : byte
    {
        /// <summary>可預約。</summary>
        [Display(Name = "可預約")]
        Available = 0,

        /// <summary>已被預約：ReservationSlots 已有他人佔用此時段。</summary>
        [Display(Name = "已預約")]
        Booked = 1,

        /// <summary>場地維護：VenueUnavailableSlots 已將此時段標記為不可用。</summary>
        [Display(Name = "維護中")]
        Unavailable = 2,

        /// <summary>
        /// 已過時間：日期為今天且該時段已開始或結束。
        /// 今天可以預約，但不能預約今天已經過去的時段。
        /// </summary>
        [Display(Name = "已過時間")]
        Past = 3,

        /// <summary>未營業：該時段落在營業時間之外，或當天公休。</summary>
        [Display(Name = "未營業")]
        Closed = 4
    }
}