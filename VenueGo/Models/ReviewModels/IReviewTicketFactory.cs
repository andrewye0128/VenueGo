namespace VenueGo.Models.ReviewModels
{
    // ════════════════════════════════════════════════════════
    //  評論憑證工廠：拆成兩個介面，而不是一個大的
    //
    //  理由（介面隔離原則 ISP）：
    //  這三個方法的呼叫者是「兩組不同的人」——
    //    現場評論憑證 + 離場時間校正 → 報到系統呼叫
    //    預約評論憑證               → 訂單／付款系統呼叫
    //
    //  如果合成一個介面，報到系統注入之後 IntelliSense 會跳出
    //  CreateReviewPerBookingAsync，他不知道那是不是該他叫的，
    //  可能在錯的時機叫下去。拆開之後各自只看得到自己該用的方法。
    //
    //  實作仍然是同一個 ReviewTicketFactory 類別，一份程式碼兩個門。
    //
    //  ⚠️ 方法名一律以 Async 結尾，這是 .NET 的慣例：
    //     看到 Async 就知道回傳的是 Task、必須 await。
    //     介面一旦交給別組就很難改名，所以在交出去之前定型。
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// 給「報到系統」呼叫。報到與離場的流程結束後，用這個建立／更新現場評論憑證。
    /// </summary>
    public interface IVisitReviewTicketFactory
    {
        /// <summary>
        /// 建立現場評論資格憑證。
        /// ⚠️ 呼叫時機：票券狀態已改為 Used 且「已經 SaveChanges」之後再呼叫。
        ///    本方法會自行 SaveChanges。
        /// </summary>
        /// <param name="token">報到票券的 QRToken（EntryTicket.Qrtoken）</param>
        /// <returns>
        /// true = 已建立憑證；
        /// false = 不符合條件或已經建立過，屬於正常情況，不是錯誤，不需要重試。
        /// </returns>
        Task<bool> CreateReviewPerVisitAsync(string? token);

        /// <summary>
        /// 校正現場評論憑證的實際離場時間。
        /// ⚠️ 呼叫時機：離場紀錄（CheckInLog）已寫入且「已經 SaveChanges」之後再呼叫。
        ///    本方法會自行 SaveChanges。
        /// </summary>
        /// <param name="ticketId">報到票券的 EntryTicket.TicketId</param>
        /// <returns>
        /// true = 已寫入離場時間；
        /// false = 查無資料、時間不合理或尚未建立評論憑證，屬正常情況，不需要重試。
        /// </returns>
        Task<bool> RecordVisitEndTimeAsync(int? ticketId);
    }

    /// <summary>
    /// 給「訂單／付款系統」呼叫。付款完成之後，用這個建立預約評論憑證。
    /// </summary>
    public interface IBookingReviewTicketFactory
    {
        /// <summary>
        /// 建立預約評論資格憑證。
        /// ⚠️ 呼叫時機：付款紀錄的 PaidAt 已寫入且「已經 SaveChanges」之後再呼叫。
        ///    本方法會自行 SaveChanges。
        /// ⚠️ 本方法由訂單／付款系統呼叫，不是報到系統。
        /// </summary>
        /// <param name="orderId">已完成付款的 Orders.OrderId</param>
        /// <returns>
        /// true = 已建立憑證；
        /// false = 查無付款紀錄或已經建立過，屬正常情況，不是錯誤，不需要重試。
        /// </returns>
        Task<bool> CreateReviewPerBookingAsync(int? orderId);
    }
}
