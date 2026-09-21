using System;

namespace VenueGo.Models.ReservationModels
{
    /// <summary>
    /// 訂單明細的一列，對應 OrdersDetails 的一筆資料。
    /// <para>
    /// 【方案 C：一格時段一列明細】一筆預約選了四個時段，就會產生四列。
    /// 這樣做的好處是計價不需要「合併同價區段」的邏輯，
    /// DurationHours 恆為 1、Subtotal 恆等於 UnitPrice，不可能算錯；
    /// 而且每一列都記得自己是哪一格時段，
    /// 報表要分析「哪個時段最熱門、尖峰營收占比」時直接 GROUP BY 即可。
    /// </para>
    /// <para>
    /// 【對應的資料庫保護】UQ_OrdersDetails_OrderId_SlotTime 唯一索引
    /// 會擋下同一張訂單同一個時段出現兩列的情況，
    /// 因此計價迴圈若不小心把某一格算了兩次，寫入時就會失敗，
    /// 而不是讓金額偏高的訂單默默存進資料庫。
    /// </para>
    /// </summary>
    public class OrderDetailLine
    {
        /// <summary>對應的時段起始時間，寫入 OrdersDetails.SlotTime。</summary>
        public TimeOnly SlotTime { get; init; }

        /// <summary>
        /// 這一格的單價快照，寫入 OrdersDetails.UnitPrice。
        /// <para>
        /// 必須存下當時的價格，不可只存場地 Id 之後再查現價。
        /// SportTypePriceRules 對每種運動只有一列規則（沒有價格歷史），
        /// 老闆調價後若靠即時查詢，舊訂單的金額會跟著變動。
        /// </para>
        /// </summary>
        public int UnitPrice { get; init; }

        /// <summary>
        /// 這一列涵蓋的小時數，寫入 OrdersDetails.DurationHours。
        /// 方案 C 下恆為 1。保留這個屬性是因為資料庫欄位為 NOT NULL
        /// 且有 CK_OrdersDetails_DurationHours 檢查必須大於 0。
        /// </summary>
        public int DurationHours => 1;

        /// <summary>小計，寫入 OrdersDetails.Subtotal。方案 C 下恆等於 UnitPrice。</summary>
        public int Subtotal => UnitPrice * DurationHours;

        /// <summary>是否為尖峰時段。僅供畫面分組顯示，不寫入資料庫。</summary>
        public bool IsPeak { get; init; }

        /// <summary>時段顯示文字，例如「19:00 - 20:00」。</summary>
        public string TimeRangeText =>
            $"{SlotTime:HH\\:mm} - {SlotTime.AddHours(1):HH\\:mm}";
    }
}