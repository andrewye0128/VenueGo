using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Dtos;
using VenueGo.Dtos.ReviewDtos.PublicReviews;
using VenueGo.Mappers;
using VenueGo.Models.Entities;
using VenueGo.Models.ReviewModels;
using VenueGo.Services.Time;

namespace VenueGo.Services.Reviews
{
    /// <summary>
    /// <see cref="IPublicReviewQueryService"/> 的實作。
    /// 10/10 從 CReviewApiController 搬過來，上架條件、篩選、排序的規則一個字都沒改。
    /// </summary>
    public sealed class PublicReviewQueryService(dbVenueContext db, ITimeService timeService) : IPublicReviewQueryService
    {
        private readonly dbVenueContext _db = db;
        private readonly ITimeService _timeService = timeService;

        /// <summary>take 的上限（10/13）：首頁 Slide 用不到這麼多，擋住有人傳一個很大的數字。</summary>
        private const int MaxTake = 50;

        /// <inheritdoc/>
        public async Task<PublicReviewListDto> GetPublicReviewsAsync(
            int? sportTypeId, string? range, int? star, bool hasContentOnly, string? sort, int? take = null)
        {
            string rg = ReviewRange.Normalize(range);
            string so = ReviewSort.Normalize(sort);
            int? st = star is >= 1 and <= 5 ? star : null;

            var sportTabs = new List<SportTabDto> { new(null, "全部") };
            sportTabs.AddRange(await _db.SportTypes
                                        .Where(s => s.IsActive)
                                        .OrderBy(s => s.SportTypeId)
                                        .Select(s => new SportTabDto(s.SportTypeId, s.SportName))
                                        .ToListAsync());

            // 星等篩選之前的結果：分布圖與平均分數用這一份算
            var beforeStar = ApplyCardFilters(PublishedReviews(_timeService.Now), sportTypeId, rg, hasContentOnly,
                                              _timeService.Today);

            var countByStar = await beforeStar
                .GroupBy(r => r.StarRating)
                .Select(g => new { Star = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Star, x => x.Count);

            var listQuery = beforeStar;
            if (st is int s)
                listQuery = listQuery.Where(r => r.StarRating == s);

            // ⚠️ 還沒有分頁。資料量變大之後要補 Skip / Take（規格已經預留 page／totalPages）。
            // 10/13：take 有給就只取前幾則（首頁 Slide）。排序之後才取，取到的才會是「最新的幾則」
            var sorted = ApplyCardSort(listQuery, so);
            var limited = take is int t ? sorted.Take(Math.Clamp(t, 1, MaxTake)) : sorted;
            var reviews = await limited.ToListAsync();

            return new PublicReviewListDto(
                Filter: new ReviewFilterDto(sportTypeId, rg, st, hasContentOnly, so),
                Defaults: new ReviewDefaultsDto(ReviewRange.Default, ReviewSort.Default),
                Options: new ReviewOptionsDto(
                    ReviewRange.Options.Select(o => new OptionItem(o.Value, o.Text)).ToList(),
                    ReviewSort.Options.Select(o => new OptionItem(o.Value, o.Text)).ToList()),
                SportTabs: sportTabs,
                Summary: ReviewMapper.ToRatingSummary(countByStar),
                Items: await BuildCardsAsync(reviews),
                Page: 1,
                TotalPages: 1);
        }

        // ════════════════════════════════════════════════════════
        //  私有方法
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// 上架條件。唯一一份。
        /// 只有現場評論、非垃圾、顧客選了公開，而且「已回覆」或「建立超過緩衝天數」。
        /// 最後那條是刻意的：館方不作為的預設結果是公開，不能用不回覆壓著負評。
        /// 首頁的評價卡 slide 之後也從這裡出發。
        /// </summary>
        private IQueryable<ReviewMain> PublishedReviews(DateTime now)
        {
            DateTime cutoff = now.AddDays(-ReviewPolicy.PublicBufferDays);

            // 10/12：時間在未來的評論（時光機撥回來之後）不上架。也擋住「近 7 天」這類範圍只篩下限、沒篩上限的問題
            return _db.ReviewMains.Where(ReviewTimeline.ReviewHappenedBy(now)).Where(r =>
                r.ReviewPerVisitId != null
                && r.SpamMarkedAt == null
                && r.IsPublic
                && (r.RepliedAt != null || r.CreatedAt <= cutoff));
        }

        /// <summary>類型、時間範圍、有無內容。星等「不」放進來——分布圖要用這個結果算。</summary>
        private IQueryable<ReviewMain> ApplyCardFilters(
            IQueryable<ReviewMain> q, int? sportTypeId, string range, bool hasContentOnly, DateTime todayStart)
        {
            if (sportTypeId is int st)
            {
                var visitIds = _db.ReviewPerVisits
                                  .Where(v => v.SportTypeId == st)
                                  .Select(v => v.ReviewPerVisitId);
                q = q.Where(r => visitIds.Contains(r.ReviewPerVisitId!.Value));
            }

            int? days = ReviewRange.DaysOf(range);
            if (days != null)
            {
                DateTime from = todayStart.AddDays(-days.Value);
                q = q.Where(r => r.CreatedAt >= from);
            }

            if (hasContentOnly)
                q = q.Where(r => r.ReviewContent != null && r.ReviewContent != "");

            return q;
        }

        private static IQueryable<ReviewMain> ApplyCardSort(IQueryable<ReviewMain> q, string sort) => sort switch
        {
            ReviewSort.Newest  => q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.StarRating),
            ReviewSort.Highest => q.OrderByDescending(r => r.StarRating).ThenByDescending(r => r.CreatedAt),
            ReviewSort.Lowest  => q.OrderBy(r => r.StarRating).ThenByDescending(r => r.CreatedAt),
            _                  => q.OrderByDescending(r => r.CreatedAt)
        };

        /// <summary>
        /// 組卡片。跨表查詢照舊：收集 id → 每張表各查一次 → Dictionary 取值（不要一則一則查）。
        /// </summary>
        private async Task<List<PublicReviewCardDto>> BuildCardsAsync(List<ReviewMain> reviews)
        {
            if (reviews.Count == 0) return new List<PublicReviewCardDto>();

            var visitIds = reviews.Where(r => r.ReviewPerVisitId != null)
                                  .Select(r => r.ReviewPerVisitId!.Value)
                                  .Distinct().ToList();
            var venueIdByVisit = await _db.ReviewPerVisits
                                          .Where(v => visitIds.Contains(v.ReviewPerVisitId))
                                          .ToDictionaryAsync(v => v.ReviewPerVisitId, v => v.VenueId);

            var venueIds = venueIdByVisit.Values.Distinct().ToList();
            var venueNames = await _db.Venues
                                      .Where(v => venueIds.Contains(v.VenueId))
                                      .ToDictionaryAsync(v => v.VenueId, v => v.VenueName);

            var userIds = reviews.Where(r => !r.IsAnonymous && r.UserId != null)
                                 .Select(r => r.UserId!.Value)
                                 .Distinct().ToList();
            var userNames = await _db.Users
                                     .Where(u => userIds.Contains(u.UserId))
                                     .ToDictionaryAsync(u => u.UserId, u => u.Name);

            return reviews.Select(r =>
            {
                string? venueName = r.ReviewPerVisitId is int vid && venueIdByVisit.TryGetValue(vid, out int venueId)
                    ? venueNames.GetValueOrDefault(venueId)
                    : null;
                string? memberName = r.UserId is int uid ? userNames.GetValueOrDefault(uid) : null;

                return ReviewMapper.ToPublicCard(r, venueName, memberName);
            }).ToList();
        }
    }
}
