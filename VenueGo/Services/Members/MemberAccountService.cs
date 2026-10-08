using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;
using VenueGo.ViewModels.MemberViewModels;

namespace VenueGo.Services.Members
{
    public class MemberAccountService : IMemberAccountService
    {
        // [搬移自 MemberController] 原本 Index 完全沒有限制 pageSize：
        // 傳 0 會造成除以零（總頁數變成亂數）、傳負數會讓 Take(負數) 直接丟例外。
        // 這裡跟員工清單（EmployeeAccountService）一致，限制在 1～100。
        private const int MinPageSize = 1;
        private const int MaxPageSize = 100;

        // 管理員可以設定的狀態值白名單（原本是任意字串都能寫進 Users.Status）
        private static readonly string[] ManageableStatuses =
            { UserStatuses.Active, UserStatuses.Suspended, UserStatuses.Inactive };

        // 看起來像電話號碼（允許 + 開頭），匯出 CSV 時不當成公式處理
        private static readonly Regex PhoneLike = new(@"^[+\d][\d\s\-()]*$", RegexOptions.Compiled);

        private readonly dbVenueContext _db;
        private readonly ILogger<MemberAccountService> _logger;
        private readonly ITimeService _time;

        public MemberAccountService(dbVenueContext db, ILogger<MemberAccountService> logger, ITimeService time)
        {
            _db = db;
            _logger = logger;
            _time = time;
        }

        // ============================================================
        // 查詢類方法
        // ============================================================

        public async Task<UserListViewModel> GetMemberListAsync(string? keyword, string? status, int page, int pageSize)
        {
            pageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);
            page = Math.Max(1, page);

            // 1. 查詢所有擁有 Member 角色的 UserId 集合
            var memberUserIds = MemberUserIdsQuery();

            // 2. 以 Users 資料表為主，條件為「UserId 存在於會員角色集合中」
            var baseQuery = _db.Users
                .Where(u => memberUserIds.Contains(u.UserId));

            // 3. 關鍵字搜尋 (姓名、Email、電話)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var trimKeyword = keyword.Trim().ToLower();
                baseQuery = baseQuery.Where(u =>
                    u.Name.ToLower().Contains(trimKeyword) ||
                    u.Email.ToLower().Contains(trimKeyword) ||
                    (u.Phone != null && u.Phone.Contains(trimKeyword))
                );
            }

            // 4. 狀態篩選
            if (!string.IsNullOrWhiteSpace(status))
            {
                baseQuery = baseQuery.Where(u => u.Status == status);
            }

            // 5. 計算總筆數與總頁數
            var totalCount = await baseQuery.CountAsync();
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

            // 6. 排序與分頁
            var rawList = await baseQuery
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserListItemDto
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    Email = u.Email,
                    Phone = u.Phone ?? "未提供",
                    IsEmployee = _db.Employees.Any(e => e.UserId == u.UserId),
                    Status = u.Status,
                    LockedUntil = u.LockedUntil,
                    CreatedAt = u.CreatedAt,
                    Roles = new List<string> { RoleNames.Member }
                }).ToListAsync();

            return new UserListViewModel
            {
                Keyword = keyword,
                SelectedStatus = status,
                SelectedUserType = "member",
                Users = rawList,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalCount = totalCount
            };
        }

        public async Task<MemberDetailViewModel?> GetMemberDetailAsync(int userId)
        {
            // 先只撈原始欄位，格式化放到記憶體做，不依賴 EF 能不能翻譯 ToString(format)
            var row = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    u.UserId,
                    u.Name,
                    u.Email,
                    u.Phone,
                    u.Birth,
                    u.CumulativeConsumption,
                    u.CumulativeVisitTime,
                    u.NoShowCount,
                    u.Status,
                    u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (row == null) return null;

            return new MemberDetailViewModel
            {
                UserId = row.UserId,
                Name = row.Name,
                Email = row.Email,
                Phone = row.Phone,
                Birth = row.Birth != default ? row.Birth.ToString("yyyy-MM-dd") : "未填寫",
                CumulativeConsumption = row.CumulativeConsumption,
                CumulativeVisitTime = row.CumulativeVisitTime,
                NoShowCount = row.NoShowCount,
                Status = row.Status,
                CreatedAt = row.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            };
        }

        // ============================================================
        // 寫入類方法
        // ============================================================

        public async Task<ServiceResult<string>> UpdateMemberStatusAsync(int userId, string status, int currentUserId)
        {
            // 白名單驗證，並統一成標準拼法（"active" → "Active"），避免資料庫裡出現多種大小寫
            var canonicalStatus = ManageableStatuses.FirstOrDefault(s =>
                string.Equals(s, status?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (canonicalStatus == null)
            {
                return ServiceResult<string>.ValidationFailed("Status", "無效的會員狀態值。");
            }

            // [新增] 管理員不可把自己停權／註銷：EmployeeAuthorizeFilter 每次請求都會檢查
            // Users.Status 必須為 Active，自己把自己停權等於立刻被鎖在系統外。
            if (userId == currentUserId && canonicalStatus != UserStatuses.Active)
            {
                return ServiceResult<string>.ValidationFailed(string.Empty, "不可將自己的帳號停權或註銷，以免無法再登入系統。");
            }

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return ServiceResult<string>.NotFound();

            user.Status = canonicalStatus;
            user.UpdatedAt = _time.Now;

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = AuditActions.UpdateMemberStatus,
                EntityType = AuditEntityTypes.User,
                EntityId = user.UserId.ToString(),
                NewValue = $"Updated member {user.Email} status to '{canonicalStatus}'",
                CreatedAt = _time.Now
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update member status for UserId {UserId}", userId);
                return ServiceResult<string>.Error("更新會員狀態時發生錯誤，請稍後再試。");
            }

            return ServiceResult<string>.Success(user.Name);
        }

        public async Task<ServiceResult<string>> ResetPasswordAsync(int userId, int currentUserId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return ServiceResult<string>.NotFound();

            user.PasswordHash = PasswordHelper.HashPassword(IMemberAccountService.DefaultResetPassword);
            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            user.UpdatedAt = _time.Now;

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = AuditActions.ResetMemberPassword,
                EntityType = AuditEntityTypes.User,
                EntityId = user.UserId.ToString(),
                NewValue = $"Reset password for member {user.Email}",
                CreatedAt = _time.Now
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset password for UserId {UserId}", userId);
                return ServiceResult<string>.Error("重設密碼時發生錯誤，請稍後再試。");
            }

            return ServiceResult<string>.Success(user.Name);
        }

        public async Task<ServiceResult<string>> UnlockAccountAsync(int userId, int currentUserId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return ServiceResult<string>.NotFound();

            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            user.UpdatedAt = _time.Now;

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = AuditActions.UnlockMemberAccount,
                EntityType = AuditEntityTypes.User,
                EntityId = user.UserId.ToString(),
                NewValue = $"Unlocked account for member {user.Email}",
                CreatedAt = _time.Now
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unlock account for UserId {UserId}", userId);
                return ServiceResult<string>.Error("解鎖帳號時發生錯誤，請稍後再試。");
            }

            return ServiceResult<string>.Success(user.Name);
        }

        public async Task<byte[]> ExportMembersCsvAsync()
        {
            var memberUserIds = MemberUserIdsQuery();

            // 只撈 CSV 需要的欄位，不把整個 User 實體（含 PasswordHash）載進記憶體
            var members = await _db.Users
                .AsNoTracking()
                .Where(u => memberUserIds.Contains(u.UserId))
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new
                {
                    u.UserId,
                    u.Name,
                    u.Email,
                    u.Phone,
                    u.Status,
                    u.CumulativeConsumption,
                    u.CreatedAt
                })
                .ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("會員ID,姓名,Email,電話,狀態,累計消費,註冊時間");

            foreach (var m in members)
            {
                builder.AppendLine(string.Join(",",
                    m.UserId,
                    CsvField(m.Name),
                    CsvField(m.Email),
                    CsvField(m.Phone, allowPhonePrefix: true),
                    CsvField(m.Status),
                    m.CumulativeConsumption,
                    $"\"{m.CreatedAt:yyyy-MM-dd HH:mm}\""));
            }

            return Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(builder.ToString()))
                .ToArray();
        }

        // ============================================================
        // Private Helpers
        // ============================================================

        /// <summary>所有擁有 Member 角色的 UserId（子查詢，不會真的執行）。</summary>
        private IQueryable<int> MemberUserIdsQuery()
        {
            return _db.UserRoles
                .Where(ur => _db.Roles.Any(r => r.RoleId == ur.RoleId && r.RoleName == RoleNames.Member))
                .Select(ur => ur.UserId);
        }

        /// <summary>
        /// 把一個值包成安全的 CSV 欄位。
        /// <list type="bullet">
        /// <item>雙引號要連寫兩個（原本沒跳脫，姓名含 " 時整列欄位會錯位）。</item>
        /// <item>以 =、+、-、@ 開頭的內容前面補一個單引號，避免 Excel 開啟時當成公式執行（CSV Injection）。</item>
        /// </list>
        /// </summary>
        private static string CsvField(string? value, bool allowPhonePrefix = false)
        {
            var s = value ?? string.Empty;

            bool looksLikeFormula = s.Length > 0 && (s[0] is '=' or '+' or '-' or '@' or '\t' or '\r');
            bool isPlainPhone = allowPhonePrefix && PhoneLike.IsMatch(s);

            if (looksLikeFormula && !isPlainPhone)
            {
                s = "'" + s;
            }

            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}