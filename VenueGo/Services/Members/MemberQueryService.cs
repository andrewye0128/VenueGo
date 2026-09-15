using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using VenueGo.ViewModels.ReservationViewModels;
using VenueGo.ViewModels.Shared;
using VenueGo.Services.Members;

namespace VenueGo.Services.Members
{
    /// <summary>
    /// 會員查詢服務的實作。
    /// <para>
    /// 【資料庫未建立外鍵】依 SQL 課程的規劃，本專案不實際建立外鍵約束，
    /// 因此 Entity 沒有導覽屬性，角色的篩選改用子查詢（Any）而非 join。
    /// 用 Any 而不是 join 還有一個好處：一位使用者若同時具有多個角色，
    /// join 會讓同一位使用者重複出現在清單中。
    /// </para>
    /// </summary>
    public class MemberQueryService : IMemberQueryService
    {
        private readonly dbVenueContext _db;

        public MemberQueryService(dbVenueContext db)
        {
            _db = db;
        }

        

        public async Task<PagedResult<MemberListItemViewModel>> SearchAsync(
            MemberSearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            criteria.Normalize();

            var query = BuildMemberQuery(criteria.Keyword, criteria.IncludeInactive);

            var totalCount = await query.CountAsync(cancellationToken);

            // 頁碼超過總頁數時（例如搜尋後頁碼還停在第 5 頁）修正回最後一頁，
            // 否則畫面會顯示一片空白，使用者會以為查無資料。
            var totalPages = totalCount == 0
                ? 1
                : (int)Math.Ceiling(totalCount / (double)criteria.PageSize);
            if (criteria.Page > totalPages) criteria.Page = totalPages;

            var rows = await query
                .OrderBy(u => u.UserId)
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize)
                .Select(u => new MemberRow
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    Phone = u.Phone,
                    Email = u.Email,
                    Status = u.Status,
                    LockedUntil = u.LockedUntil,
                    CarrierNo = u.CarrierNo
                })
                .ToListAsync(cancellationToken);

            var now = DateTime.Now;
            var items = rows.Select(r => ToViewModel(r, now)).ToList();

            return new PagedResult<MemberListItemViewModel>
            {
                Items = items,
                Page = criteria.Page,
                PageSize = criteria.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<MemberListItemViewModel?> GetSelectableMemberAsync(
            int userId, CancellationToken cancellationToken = default)
        {
            var row = await BuildMemberQuery(keyword: null, includeInactive: true)
                .Where(u => u.UserId == userId)
                .Select(u => new MemberRow
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    Phone = u.Phone,
                    Email = u.Email,
                    Status = u.Status,
                    LockedUntil = u.LockedUntil,
                    CarrierNo = u.CarrierNo
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null) return null;

            var viewModel = ToViewModel(row, DateTime.Now);

            // 刻意回傳 null 而非不可選的物件：
            // 呼叫端只需判斷 null，不必再重複一次可選性的判斷邏輯。
            return viewModel.IsSelectable ? viewModel : null;
        }

        /// <summary>
        /// 組出會員查詢的共用條件：必須具有一般會員角色，
        /// 並依關鍵字與是否包含停權會員做篩選。
        /// </summary>
        private IQueryable<User> BuildMemberQuery(string? keyword, bool includeInactive)
        {
            var query = _db.Users.AsNoTracking()   // AsNoTracking()：告訴 EF「我只是要讀出來顯示，不會改它」。EF 就不用替每筆資料做變更追蹤，查詢會比較快、比較省記憶體。純查詢的服務幾乎都該加。
                .Where(u => _db.UserRoles.Any(
                    ur => ur.UserId == u.UserId && ur.RoleId == (int)RoleType.Member));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(u =>
                    u.Name.Contains(keyword) ||
                    u.Phone.Contains(keyword) ||
                    u.Email.Contains(keyword));
            }

            if (!includeInactive)
            {
                query = query.Where(u => u.Status == UserStatuses.Active);
            }

            return query;
        }

        /// <summary>
        /// 將查詢結果轉為 ViewModel，並在此處集中判斷「是否可被選為預約人」。
        /// 以傳入的 now 為判斷基準，確保同一次查詢中每一列的判斷時間一致。
        /// </summary>
        private static MemberListItemViewModel ToViewModel(MemberRow row, DateTime now)
        {
            string? reason = null;

            if (row.Status != UserStatuses.Active)
            {
                reason = row.Status == UserStatuses.Suspended
                    ? "此會員已停權，無法建立預約。"
                    : "此會員帳號已註銷，無法建立預約。";
            }
            else if (row.LockedUntil.HasValue && row.LockedUntil.Value > now)
            {
                reason = $"此會員帳號鎖定至 {row.LockedUntil.Value:yyyy/MM/dd HH:mm}，無法建立預約。";
            }

            return new MemberListItemViewModel
            {
                UserId = row.UserId,
                Name = row.Name,
                Phone = row.Phone,
                Email = row.Email,
                Status = row.Status,
                LockedUntil = row.LockedUntil,
                CarrierNo = row.CarrierNo,
                IsSelectable = reason is null,
                UnselectableReason = reason
            };
        }

        /// <summary>
        /// 資料庫投影用的中繼型別。
        /// <para>
        /// 之所以不直接投影成 MemberListItemViewModel，是因為可選性的判斷
        /// 需要比對系統時間並組出中文訊息，這些是 EF Core 無法翻譯成 SQL 的。
        /// 先取出必要欄位，再於記憶體中轉換，可避免整張表被載入。
        /// </para>
        /// </summary>
        private sealed class MemberRow
        {
            public int UserId { get; init; }
            public string Name { get; init; } = string.Empty;
            public string Phone { get; init; } = string.Empty;
            public string Email { get; init; } = string.Empty;
            public string Status { get; init; } = string.Empty;
            public DateTime? LockedUntil { get; init; }
            public string? CarrierNo { get; init; }
        }
    }
}