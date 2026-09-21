using System;
using System.Collections.Generic;
using VenueGo.Models.Enums;

namespace VenueGo.ViewModels.ReservationViewModels
{

    /// <summary>
    /// 新增預約流程（五個步驟）的暫存資料，存放於 Session。
    /// <para>
    /// 【為什麼需要這個類別】新增預約有五個步驟、五個頁面，
    /// 但直到步驟 5 按下「建立預約」才會真正寫入資料庫。
    /// 前四個步驟選的東西必須有地方暫存，這個類別就是那個容器。
    /// </para>
    /// <para>
    /// 【為什麼不先寫進資料庫當草稿】Reservations 有多個 NOT NULL 欄位
    /// （StartTime、EndTime、PaymentDueAt、TermsAcceptedAt 等），
    /// 步驟 1 時根本填不出來；而且草稿會佔用 ReservationSlots 的唯一鍵，
    /// 等於還沒確定就先卡住別人的時段。
    /// </para>
    /// <para>
    /// 【為什麼存了會員姓名等顯示欄位】右側「預約資訊摘要」每一步都要顯示
    /// 會員姓名與場地名稱。若只存 Id，每換一頁就要多查一次資料庫。
    /// 這裡刻意存冗餘的顯示欄位換取效能與程式簡潔，
    /// 但它們只用於畫面顯示，最終寫入資料庫時一律以 Id 重新查詢為準。
    /// </para>
    /// </summary>

    public class ReservationDraft
    {
        // ── 步驟 1：選擇會員 ──────────────────────────────

        /// <summary>預約的會員（Reservations.UserId）。</summary>
        public int? UserId { get; set; }

        /// <summary>會員編號，由 UserId 格式化而來，僅供顯示。</summary>
        public string? MemberNo { get; set; }

        /// <summary>會員姓名，僅供顯示。</summary>
        public string? MemberName { get; set; }

        /// <summary>會員手機，僅供顯示。</summary>
        public string? MemberPhone { get; set; }

        /// <summary>會員 Email，僅供步驟 5 確認頁顯示。</summary>
        public string? MemberEmail { get; set; }

        /// <summary>會員已登錄的手機條碼載具，供步驟 5 自動帶入。</summary>
        public string? MemberCarrierNo { get; set; }

        // ── 步驟 2：選擇場地 ──────────────────────────────

        /// <summary>場地（Reservations.VenueId）。</summary>
        public int? VenueId { get; set; }

        /// <summary>場地名稱，僅供顯示。</summary>
        public string? VenueName { get; set; }

        /// <summary>場地可容納人數，供步驟 5 驗證使用人數上限。</summary>
        public int? VenueCapacity { get; set; }

        // ── 步驟 3：選擇日期 ──────────────────────────────

        /// <summary>使用日期（Reservations.BookingDate）。</summary>
        public DateOnly? BookingDate { get; set; }

        // ── 步驟 4：選擇時段 ──────────────────────────────

        /// <summary>
        /// 已選時段的起始時間清單，每一項對應一列 ReservationSlots。
        /// 必須為連續時段，連續性驗證在步驟 4 的 Service 內執行。
        /// </summary>
        public List<TimeOnly> SlotTimes { get; set; } = new();

        /// <summary>預估總金額，由後端計價後寫入，絕不接受前端傳入的金額。</summary>
        public int EstimatedAmount { get; set; }

        // ── 步驟 5：確認資料 ──────────────────────────────

        /// <summary>使用人數（Orders.PersonAmount），預設 1。</summary>
        public int PersonAmount { get; set; } = 1;

        /// <summary>發票類型（Orders.InvoiceType）。</summary>
        public InvoiceType InvoiceType { get; set; } = InvoiceType.Paper;

        /// <summary>手機條碼載具（Orders.CarrierNo），發票類型為紙本時必須為 null。</summary>
        public string? CarrierNo { get; set; }

        /// <summary>付款方式（Payments.PaymentMethod）。後台代客建立固定為現場付款。</summary>
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.OnSite;

        /// <summary>付款通道（Payments.PaymentChannel）。後台代客建立固定為現場櫃台。</summary>
        public PaymentChannel PaymentChannel { get; set; } = PaymentChannel.Counter;

        /// <summary>管理員是否已確認向會員說明並同意租借條款。未勾選不可建立預約。</summary>
        public bool TermsAccepted { get; set; }

        // ── 流程控制 ──────────────────────────────────────

        /// <summary>步驟 1 是否已完成。</summary>
        public bool IsMemberSelected => UserId.HasValue;

        /// <summary>步驟 2 是否已完成。</summary>
        public bool IsVenueSelected => VenueId.HasValue;

        /// <summary>步驟 3 是否已完成。</summary>
        public bool IsDateSelected => BookingDate.HasValue;

        /// <summary>步驟 4 是否已完成。</summary>
        public bool IsSlotSelected => SlotTimes.Count > 0;

        /// <summary>
        /// 目前可進入的最大步驟編號（1~5）。
        /// 用於防止使用者直接打網址跳到後面的步驟，
        /// 例如尚未選會員就開啟步驟 4 的網址。
        /// </summary>
        public int MaxAllowedStep
        {
            get
            {
                if (!IsMemberSelected) return 1;
                if (!IsVenueSelected) return 2;
                if (!IsDateSelected) return 3;
                if (!IsSlotSelected) return 4;
                return 5;
            }
        }
    }
}
