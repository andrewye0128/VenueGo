namespace VenueGo.ViewModels.ReviewViewModels
{
    /// <summary>館方清單裡的一則評論。顯示用，全部 init。</summary>
    public sealed record ReviewQueueItemVM
    {
        public int ReviewId { get; init; }
        public byte StarRating { get; init; }
        public DateTime CreatedAt { get; init; }
        public required string DisplayName { get; init; }
        public bool IsAnonymous { get; init; }
        public bool IsPublic { get; init; }
        public string? Content { get; init; }
        public bool MentionsVenue { get; init; }
        public bool MentionsStaff { get; init; }

        // ── 來源：現場評論有場地與時段，預約評論有訂單編號 ──
        public bool IsBookingReview { get; init; }
        public string? VenueName { get; init; }

        /// <summary>只有現場評論有。場地名稱做成「只看這個場地」的按鈕時要用。</summary>
        public int? VenueId { get; init; }

        public DateTime? RentStartTime { get; init; }

        /// <summary>
        /// 訂單編號。兩種評論都會有值：
        ///   預約評論 → ReviewPerBooking.OrderId
        ///   現場評論 → ReviewPerVisit.Qrtoken 對到 EntryTicket，再拿它的 OrderId
        /// 追不到訂單時是 null（資料異常，不該發生但要擋得住）。
        /// </summary>
        public string? OrderNo { get; init; }

        /// <summary>分組用。顯示用的是 OrderNo，這個是拿來 GroupBy 的鍵。</summary>
        public int? OrderId { get; init; }

        /// <summary>
        /// 10/27：這筆訂單的預約編號（Orders.ReservationId）。訂單編號做成連結，連到預約組的預約詳細頁（/Reservation/Details/{id}）。
        /// 追不到訂單、或訂單沒有預約（Orders.ReservationId 是 null）時是 null，訂單編號就只顯示文字。
        /// </summary>
        public int? ReservationId { get; init; }

        /// <summary>
        /// 10/25：預約評論的付款方式（現場櫃台／信用卡／LINE Pay），只有預約評論會填。
        /// 預約評論評的是「預約流程」，跟場地、使用時段無關，所以館方清單改顯示付款方式。
        /// 查不到已付款的紀錄時是 null，畫面顯示「查無付款紀錄」。
        /// </summary>
        public string? PaymentChannelName { get; init; }

        // ── 處理狀態 ──
        public DateTime? ReadAt { get; init; }
        public string? ReadByEmployeeName { get; init; }
        public bool IsPinned { get; init; }

        // ── 回覆（已完成／全部清單要看的）──
        public DateTime? RepliedAt { get; init; }
        public string? ReplyContent { get; init; }
        public string? RepliedByEmployeeName { get; init; }

        /// <summary>顧客看到回覆的時間。null 代表還沒看到，與「看到了但不表態」不同。</summary>
        public DateTime? ReplyViewedAt { get; init; }

        /// <summary>0 不滿意 / 1 普通 / 2 滿意，null 是還沒表態。</summary>
        public byte? ReplySatisfaction { get; init; }

        // ── 垃圾標記 ──
        public DateTime? SpamMarkedAt { get; init; }
        public string? SpamReasonText { get; init; }
        public string? SpamMarkedByEmployeeName { get; init; }

        /// <summary>
        /// 由 Controller 用「同一個現在時間」算好再放進來。
        /// 不寫成計算屬性，是因為那樣每讀一次就取一次 DateTime.Now，
        /// 同一頁上每則評論的判斷基準會不一樣。
        /// </summary>
        public OverdueLevel OverdueLevel { get; init; }

        // ── 10/26 負評優先：現在該做什麼、期限（Controller 用同一個「現在」算好）──
        public DeadlineStage Stage { get; init; }

        /// <summary>目前這個階段的目標期限。已處理的是 null。未讀、待回覆清單依這個排序。</summary>
        public DateTime? DueAt { get; init; }

        /// <summary>期限的文字，例如「今天 18:00」。已處理的是 null。</summary>
        public string? DueText { get; init; }

        /// <summary>已經過了目標期限（還沒處理的才會是 true）。</summary>
        public bool IsPastDue { get; init; }

        // ── 計算屬性 ──
        public bool IsRead => ReadAt != null;
        public bool HasReply => RepliedAt != null;
        public bool IsReplyViewed => ReplyViewedAt != null;
        public bool IsSpam => SpamMarkedAt != null;
        public bool IsRatingOnly => string.IsNullOrWhiteSpace(Content);

        /// <summary>
        /// 已超過緩衝天數、沒回覆、顧客選了公開 → 依規則已經出現在評論專區。
        /// 預約評論的 IsPublic 一律是 false，所以不必另外排除。
        /// </summary>
        public bool IsAutoPublished => OverdueLevel == OverdueLevel.Overdue && IsPublic;

        /// <summary>摘要列的前 30 字。</summary>
        public string Summary
        {
            get
            {
                if (IsRatingOnly) return "（僅評分，未留言）";
                return Content!.Length <= 30 ? Content : Content[..30] + "…";
            }
        }
    }
}