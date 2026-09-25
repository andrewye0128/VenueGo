namespace VenueGo.Models.Enums
{
    public enum EntryTicketStatus : byte
    {
        Valid = 1,      // 有效、未入場
        Used = 2,       // 已入場使用
        Expired = 3,    // 已失效（未使用失效 / 超時失效，原因由入場紀錄判斷）
        Cancelled = 4,  // 已取消（退款）
        Completed = 5,  // 已完成（使用完畢且已離場）
    }
}
