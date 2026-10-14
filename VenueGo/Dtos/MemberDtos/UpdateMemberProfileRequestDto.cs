using System.ComponentModel.DataAnnotations;

namespace VenueGo.Dtos.MemberDtos
{
    /// <summary>
    /// PUT /api/member/profile 的請求內容：前台會員修改自己的個人資料。
    /// <para>
    /// 【為何不直接用 UserProfileViewModel】那是後台「員工編輯自己」用的，
    /// 內含員工編號、職稱、角色等會員用不到的欄位，Email 還是 [Required]（會員端明明不能改，
    /// 卻得硬傳）。請求型別只放「這個端點允許改的欄位」，多送的欄位不會被綁定進來。
    /// </para>
    /// <para>
    /// 【刻意沒有的欄位】
    /// UserId：一律來自 Token（ICurrentUserService），不收前端傳的，避免改別人的資料。
    /// Email：它是登入帳號，變更需要驗證新信箱，這個端點不處理。
    /// ConfirmNewPassword：「兩次輸入是否一致」是畫面的事，由前端檢查就好，API 不需要收。
    /// </para>
    /// </summary>
    public class UpdateMemberProfileRequestDto
    {
        [Required(ErrorMessage = "請輸入姓名")]
        [StringLength(50, ErrorMessage = "姓名長度不能超過 50 個字")]
        public string Name { get; set; } = string.Empty;

        // 規則與前台畫面的檢查一致：09 開頭共 10 碼
        [Required(ErrorMessage = "請輸入手機號碼")]
        [RegularExpression(@"^09\d{8}$", ErrorMessage = "手機號碼格式不正確，請輸入 09 開頭的 10 碼手機號碼")]
        public string Phone { get; set; } = string.Empty;

        // 選填。空白或 null 代表清除載具。
        // 格式（斜線加 7 碼大寫英數字）需要先轉大寫再檢查，所以放在 Service 裡判斷。
        [StringLength(20, ErrorMessage = "載具號碼長度不正確")]
        public string? CarrierNo { get; set; }

        // ── 修改密碼（選填）──
        // 【為何這裡只有長度上限、沒有最小長度與格式規則】
        // 前端用 JSON 送「留空」會是空字串 ""，而 [StringLength(MinimumLength = 8)] 會把 "" 判成太短，
        // 使用者只想改姓名卻被擋下來。所以「空白代表不改密碼」，
        // 有填新密碼時才由 Service 統一檢查：必須有舊密碼、舊密碼正確、新密碼長度與組成。
        [StringLength(100, ErrorMessage = "密碼長度不正確")]
        public string? CurrentPassword { get; set; }

        [StringLength(100, ErrorMessage = "密碼長度不正確")]
        public string? NewPassword { get; set; }
    }
}