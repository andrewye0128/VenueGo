namespace VenueGo.Models.ReservationModels
{
    /// <summary>
    /// 費用明細「尖峰／離峰」標籤的共用規則：後台預約詳細頁、前台訂單明細都用這一份。
    /// <para>
    /// 【先用存下來的值】下單時已經把「這格是不是尖峰」存進 OrdersDetails.IsPeak，
    /// 之後尖峰時間、價格怎麼改，舊訂單的標籤都不會變。
    /// IsPeak 是 NULL（加上這個欄位之前的舊資料）才用「預約當天的尖峰起始時間」推算，
    /// 推算用的是今天的設定，設定改過的話可能跟下單當下不一樣。
    /// </para>
    /// <para>
    /// 只用在「顯示」。計價時的尖峰判斷在 TimeSlotService.BuildSlotStatus，金額以 OrdersDetails.UnitPrice 為準。
    /// </para>
    /// </summary>
    public static class PeakSlotRules
    {
        /// <summary>
        /// 這一格是不是尖峰：有存下來的值就用它；沒有才推算（起始時間 &gt;= 那天的尖峰起始時間）。
        /// </summary>
        /// <param name="storedIsPeak">下單時存下來的值（OrdersDetails.IsPeak），舊資料是 null。</param>
        /// <param name="slotTime">這一格的起始時間（OrdersDetails.SlotTime，舊資料可能是 null）。</param>
        /// <param name="peakStartTime">
        /// 預約當天的尖峰起始時間，請用 IVenueScheduleService.GetPeakStartTimeAsync 取得。
        /// null 代表那天不分尖峰／離峰，或是不需要推算（每一格都有存下來的值）。
        /// </param>
        public static bool IsPeak(bool? storedIsPeak, TimeOnly? slotTime, TimeOnly? peakStartTime)
        {
            if (storedIsPeak.HasValue) return storedIsPeak.Value;

            if (slotTime is null || peakStartTime is null) return false;

            return slotTime.Value >= peakStartTime.Value;
        }

        /// <summary>
        /// 這些明細裡，有沒有哪一格需要推算（IsPeak 是 null）。
        /// <para>
        /// 都有存下來的值，就不用向場地模組查尖峰起始時間，少查一次資料庫。
        /// </para>
        /// </summary>
        public static bool NeedsFallback(IEnumerable<bool?> storedIsPeaks)
            => storedIsPeaks.Any(isPeak => !isPeak.HasValue);
    }
}