namespace VenueGo.Models.Enums
{
    public enum CheckInAction : byte
    {
        CheckIn = 1,        // 入場
        CheckOut = 2,       // 離場
        ManualCancel = 3,   // 人工取消(尚未走退款流程)
        ManualExpire = 4,   // 人工轉逾期
    }
}
