namespace VenueGo.Models.Enums
{
    public enum CheckInFailReason : byte
    {
        TicketNotFound = 1,    // 找不到票券
        AlreadyCancelled = 2,  // 票券已取消
        AlreadyExpired = 3,    // 票券已逾期
        NotYetStartTime = 4,   // 尚未到預約時間
        InvalidSequence = 5,   // 入場/離場順序異常(例如還沒入場就要離場、已入場卻再次入場)

        AlreadyCompleted = 6,  // 票券已使用完畢
    }
}
