namespace VenueGo.Dtos.ReviewDtos
{

    /// <summary>
    /// 評論種類，也是網址的一段（/api/reviews/visit/...、/api/reviews/booking/...）。
    /// 回應裡的 kind 欄位也是這兩個值。
    /// </summary>
    public static class ReviewKind
    {
        public const string Visit = "visit";       // 現場評論，憑「現場評論憑證 Id」（ReviewPerVisitId），限會員本人
        public const string Booking = "booking";   // 預約評論，憑訂單 Id（OrderId），限訂購人本人
    }
}
