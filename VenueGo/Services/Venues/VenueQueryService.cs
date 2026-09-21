using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.ViewModels.ReservationViewModels;
using VenueGo.ViewModels.Shared;

namespace VenueGo.Services.Venues
{
    /// <summary>
    /// 場地查詢服務的實作。
    /// <para>
    /// 【資料庫未建立外鍵】Entity 沒有導覽屬性，SportTypes 的名稱與
    /// SportTypePriceRules 的價格都以手動 join 或子查詢取得。
    /// </para>
    /// </summary>
    public class VenueQueryService : IVenueQueryService
    {
        private readonly dbVenueContext _db;

        public VenueQueryService(dbVenueContext db)
        {
            _db = db;
        }

        public async Task<PagedResult<VenueCardViewModel>> SearchAsync(
            VenueSearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            criteria.Normalize();

            var query = BuildVenueQuery(criteria.SportTypeId, criteria.Keyword);

            var totalCount = await query.CountAsync(cancellationToken);

            // 頁碼超過總頁數時（例如篩選後頁碼還停在第 3 頁）修正回最後一頁，
            // 否則畫面會一片空白，使用者會誤以為查無資料。
            var totalPages = totalCount == 0
                ? 1
                : (int)Math.Ceiling(totalCount / (double)criteria.PageSize);
            if (criteria.Page > totalPages) criteria.Page = totalPages;

            var items = await query
                // 可預約的排前面，管理員不必在停用的場地之間找
                .OrderByDescending(x => x.Venue.IsActive)
                .ThenBy(x => x.Venue.VenueName)
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize)
                .Select(Project)
                .ToListAsync(cancellationToken);

            return new PagedResult<VenueCardViewModel>
            {
                Items = items,
                Page = criteria.Page,
                PageSize = criteria.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<VenueCardViewModel?> GetSelectableVenueAsync(
            int venueId, CancellationToken cancellationToken = default)
        {
            var venue = await BuildVenueQuery(sportTypeId: null, keyword: null)
                .Where(x => x.Venue.VenueId == venueId)
                .Select(Project)
                .FirstOrDefaultAsync(cancellationToken);

            if (venue is null) return null;

            // 刻意回傳 null 而非不可選的物件：
            // 呼叫端只需判斷 null，不必再重複一次可選性的判斷邏輯。
            return venue.IsSelectable ? venue : null;
        }

        public async Task<IReadOnlyList<SportTypeOption>> GetSportTypeOptionsAsync(
            CancellationToken cancellationToken = default)
        {
            return await _db.SportTypes.AsNoTracking()
                .Where(st => st.IsActive)
                .OrderBy(st => st.SportTypeId)
                .Select(st => new SportTypeOption
                {
                    SportTypeId = st.SportTypeId,
                    SportName = st.SportName
                })
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// 組出場地查詢的共用條件。
        /// <para>
        /// 這裡刻意不過濾 IsActive：停用的場地仍會顯示，但卡片上會標示「維護中」
        /// 且無法選取。原因是管理員需要知道「為什麼這個場地訂不到」，
        /// 完全隱藏會讓他以為場地被刪除了。
        /// 可選性的判斷集中在 VenueCardViewModel.IsSelectable 與
        /// GetSelectableVenueAsync。
        /// </para>
        /// </summary>
        private IQueryable<VenueQueryRow> BuildVenueQuery(int? sportTypeId, string? keyword)
        {
            var query = from v in _db.Venues.AsNoTracking()
                        join st in _db.SportTypes on v.SportTypeId equals st.SportTypeId
                        join pr in _db.SportTypePriceRules.Where(r => r.IsActive)
                            on v.SportTypeId equals pr.SportTypeId into prs
                        from pr in prs.DefaultIfEmpty()
                        select new VenueQueryRow { Venue = v, SportType = st, PriceRule = pr };

            if (sportTypeId.HasValue)
            {
                query = query.Where(x => x.Venue.SportTypeId == sportTypeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x => x.Venue.VenueName.Contains(keyword));
            }

            return query;
        }

        /// <summary>
        /// 投影成卡片 ViewModel。價格以子查詢取得，
        /// 因為 SportTypePriceRules 對每種運動只有一列（UQ_SportTypePriceRules_SportTypeId）。
        /// </summary>
        /// 
        //private VenueCardViewModel Project(VenueQueryRow row) => new()
        //{
        //    VenueId = row.Venue.VenueId,
        //    VenueName = row.Venue.VenueName,
        //    SportName = row.SportType.SportName,
        //    Capacity = row.Venue.Capacity,
        //    PhotoPath = row.Venue.PhotoPath,
        //    IsActive = row.Venue.IsActive,
        //    OffPeakPrice = _db.SportTypePriceRules
        //        .Where(r => r.SportTypeId == row.Venue.SportTypeId && r.IsActive)
        //        .Select(r => (int?)r.OffPeakPrice)
        //        .FirstOrDefault(),
        //    PeakPrice = _db.SportTypePriceRules
        //        .Where(r => r.SportTypeId == row.Venue.SportTypeId && r.IsActive)
        //        .Select(r => (int?)r.PeakPrice)
        //        .FirstOrDefault(),
        //    PeakStartTime = _db.SportTypePriceRules
        //        .Where(r => r.SportTypeId == row.Venue.SportTypeId && r.IsActive)
        //        .Select(r => r.PeakStartTime)
        //        .FirstOrDefault()
        //};



        private static readonly Expression<Func<VenueQueryRow, VenueCardViewModel>> Project =
        row => new VenueCardViewModel
        {
            VenueId = row.Venue.VenueId,
            VenueName = row.Venue.VenueName,
            SportName = row.SportType.SportName,
            Capacity = row.Venue.Capacity,
            PhotoPath = row.Venue.PhotoPath,
            IsActive = row.Venue.IsActive,
            OffPeakPrice = row.PriceRule == null ? null : (int?)row.PriceRule.OffPeakPrice,
            PeakPrice = row.PriceRule == null ? null : (int?)row.PriceRule.PeakPrice,
            PeakStartTime = row.PriceRule == null ? null : row.PriceRule.PeakStartTime
        };



        /// <summary>
        /// 查詢過程的中繼型別，讓篩選與投影可以共用同一個查詢。
        /// </summary>
        private sealed class VenueQueryRow
        {
            public Models.Entities.Venue Venue { get; init; } = null!;
            public Models.Entities.SportType SportType { get; init; } = null!;
            public Models.Entities.SportTypePriceRule? PriceRule { get; init; }
        }
    }
}
