namespace VenueGo.Models.ReservationModels
{
    /// <summary>
    /// 費用明細「尖峰／離峰」標籤的共用規則：後台預約詳細頁、前台訂單明細都用這一份。
    /// <para>
    /// 【為何獨立出來】兩個頁面的判斷完全一樣，之後 OrdersDetails 新增 IsPeak 欄位時，
    /// 也要一起改成「先讀存下來的值，沒有才推算」。寫在這裡，只要改一個地方。
    /// </para>
    /// <para>
    /// 只用在「顯示」。計價時的尖峰判斷在 TimeSlotService.BuildSlotStatus，金額以 OrdersDetails.UnitPrice 為準。
    /// </para>
    /// </summary>
    public static class PeakSlotRules
    {
        /// <summary>
        /// 這一格是不是尖峰：起始時間 &gt;= 那天的尖峰起始時間，尖峰一路延續到打烊。
        /// </summary>
        /// <param name="slotTime">這一格的起始時間（OrdersDetails.SlotTime，舊資料可能是 null）。</param>
        /// <param name="peakStartTime">
        /// 預約當天的尖峰起始時間，請用 IVenueScheduleService.GetPeakStartTimeAsync 取得。
        /// null 代表那天不分尖峰／離峰。
        /// </param>
        public static bool IsPeak(TimeOnly? slotTime, TimeOnly? peakStartTime)
        {
            if (slotTime is null || peakStartTime is null) return false;

            return slotTime.Value >= peakStartTime.Value;
        }
    }
}
