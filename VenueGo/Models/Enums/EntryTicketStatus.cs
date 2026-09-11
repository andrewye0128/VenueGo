namespace VenueGo.Models.Enums
{
    public enum EntryTicketStatus : byte
    {
        Valid = 1,      // 有效、未入場
        Used = 2,       // 已入場使用
        Expired = 3,    // 逾期未使用
        Cancelled = 4,  // 已取消（退款）
    }
}
