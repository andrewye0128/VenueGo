using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels.ReviewVM
{
    /// <summary>
    /// 顧客送出評論——兩種撰寫頁共用的部分。
    /// abstract 表示它不會被單獨 new 出來，只當作父類別使用。
    /// </summary>
    public abstract class ReviewCreateInputVM
    {
        [DisplayName("評分")]
        [Required(ErrorMessage = "請選擇星等")]
        [Range(1, 5, ErrorMessage = "星等必須介於 1 到 5")]
        public byte? StarRating { get; set; }

        [DisplayName("評論內容")]
        [StringLength(1000, ErrorMessage = "評論內容不可超過 1000 字")]
        [DataType(DataType.MultilineText)]
        public string? ReviewContent { get; set; }

        [DisplayName("提及場地")]
        public bool MentionsVenue { get; set; }

        [DisplayName("提及服務")]
        public bool MentionsStaff { get; set; }

        [DisplayName("是否要匿名")]
        public bool IsAnonymous { get; set; }

        /// <summary>預設公開。館方不作為的預設結果是公開，不是壓下來。</summary>
        [DisplayName("是否公開")]
        public bool IsPublic { get; set; } = true;

        /// <summary>
        /// 登入會員才能自己決定要不要匿名；未登入同行者沒有身分可顯示，
        /// 只能匿名（對應約束 CHK_ReviewMain_Anonymous_Logic）。
        /// 由 Controller 依 ICurrentUser.MemberId 是否為 null 填入。
        /// </summary>
        public bool CanChooseAnonymous { get; set; }

        // ── 表單最上方的確認區塊 ──────────────────────────
        //
        // 兩種憑證能提供的資訊不一樣（預約憑證根本沒有 VenueId），
        // 所以父類別只宣告「要顯示兩行字」，實際是哪兩行由子類別決定。
        // 共用的 partial view 只認得這兩個屬性，不必知道自己在
        // 處理哪一種評論。
        //
        // abstract + 唯讀：它們是算出來的，不參與模型繫結，
        // POST 回來時不需要、也不應該由表單提供。

        /// <summary>確認區塊第一行（場地名稱／訂單編號）</summary>
        public abstract string? ContextPrimary { get; }

        /// <summary>確認區塊第二行（使用時段／付款方式）</summary>
        public abstract string? ContextSecondary { get; }
    }
}
