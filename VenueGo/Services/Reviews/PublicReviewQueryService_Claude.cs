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
    /// <para>
    /// 10/15 Reviews 重構：改從檢視表 v_ReviewPublicCard 查。
    /// 檢視表已經只剩「現場評論、公開、沒被下架、周邊資料一項都不缺」的評論，
    /// 場地、運動類型、會員名稱、入場時間也都在裡面，所以不用再一張一張表去查名稱。
    /// </para>
    /// </summary>
    public sealed class PublicReviewQueryService(dbVenueContext db, ITimeService timeService) : IPublicReviewQueryService
    {
        private readonly dbVenueContext _db = db;
        private readonly ITimeService _timeService = timeService;

        /// <summary>take 的上限（10/13）：首頁 Slide 用不到這麼多，擋住有人傳一個很大的數字。</summary>
        private const int MaxTake = 50;

        /// <summary>首頁輪播最多幾張（10/15）：手機 1、平板 2、電腦 3 張一頁都整除。</summary>
        private const int FeaturedCount = 12;

        // 首頁輪播：幾個字以上算「30 字以上」的長評（10/16）
        private const int FeaturedLongTextMin = 30;

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
                Items: reviews.Select(ReviewMapper.ToPublicCard).ToList(),
                Page: 1,
                TotalPages: 1);
        }

        /// <inheritdoc/>
        public async Task<FeaturedReviewsDto> GetFeaturedReviewsAsync()
        {
            DateTime now = _timeService.Now;
            var published = PublishedReviews(now);

            // 平均分數、總則數：全部公開評價（含只給星等的），跟評論專區「全部時間」一致
            var countByStar = await published
                .GroupBy(r => r.StarRating)
                .Select(g => new { Star = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Star, x => x.Count);

            // 候選：3 星以上（10/16 起含只給星等的）。只讀挑選需要的欄位，挑好之後才讀完整的卡片。
            // （以專題的資料量全部讀回來沒問題；之後評論多到上萬則，再把階段條件搬進 SQL。）
            // 字數用 SQL 的 LEN 算：只有空白的內容會算成 0 字，跟「只給星等」一樣處理。
            var candidates = await published
                .Where(r => r.StarRating >= 3)
                .Select(r => new FeaturedCandidate(
                    r.ReviewId,
                    r.StarRating,
                    r.CreatedAt,
                    r.RepliedAt != null,                                         // HasReply
                    r.ReviewContent == null ? 0 : r.ReviewContent.Length))      // TextLength
                .ToListAsync();

            var pickedIds = PickFeatured(candidates, now, FeaturedCount, Random.Shared);

            var cards = await published.Where(r => pickedIds.Contains(r.ReviewId)).ToListAsync();
            var byId = cards.ToDictionary(c => c.ReviewId);

            return new FeaturedReviewsDto(
                Summary: ReviewMapper.ToRatingSummary(countByStar),
                // 照挑出來的（已打亂的）順序排；萬一某一則在兩次查詢之間被下架，就少那一則
                Items: pickedIds.Where(byId.ContainsKey).Select(id => ReviewMapper.ToPublicCard(byId[id])).ToList());
        }

        /// <summary>首頁輪播挑選用的最小資料。TextLength 是 0 代表只給星等。</summary>
        public sealed record FeaturedCandidate(int ReviewId, byte StarRating, DateTime CreatedAt, bool HasReply, int TextLength)
        {
            public bool IsRatingOnly => TextLength == 0;
            public bool IsLong => TextLength >= FeaturedLongTextMin;
        }

        /// <summary>
        /// 首頁輪播的挑選規則（規格 2-11，10/16 改版）。純計算、不碰資料庫，random 由外面傳進來，方便寫單元測試（傳固定種子）。
        /// <list type="number">
        ///   <item>依序看 stages 裡的每一個階段（a～e，見程式裡的註解）。後面的階段會包含前面已經選過的評論，選過的跳過，
        ///         所以每一群只要照字面寫條件，不用再寫「排除前面的」。</item>
        ///   <item>前面階段的全部保留；某一階段加進來會超過 count 時，只從這一階段裡隨機抽，補滿就停。</item>
        ///   <item>最後把全部打亂順序：每次打開首頁，看到的評論和順序都不一樣。</item>
        ///   <item>一則都沒選到時回空清單，前台會把整個「顧客評價」區塊拿掉（f）。</item>
        /// </list>
        /// </summary>
        public static List<int> PickFeatured(IReadOnlyList<FeaturedCandidate> candidates, DateTime now, int count, Random random)
        {
            DateTime threeMonthsAgo = now.AddMonths(-3);
            DateTime oneYearAgo = now.AddYears(-1);

            // 「近期四段」：近 3 個月 5 星 → 近 3 個月 4 星 → 近 1 年 5 星 → 近 1 年 4 星，套在某一群評論上
            Func<FeaturedCandidate, bool>[] Recent(Func<FeaturedCandidate, bool> group) =>
            [
                c => group(c) && c.CreatedAt >= threeMonthsAgo && c.StarRating == 5,
                c => group(c) && c.CreatedAt >= threeMonthsAgo && c.StarRating == 4,
                c => group(c) && c.CreatedAt >= oneYearAgo && c.StarRating == 5,
                c => group(c) && c.CreatedAt >= oneYearAgo && c.StarRating == 4,
            ];

            Func<FeaturedCandidate, bool>[] stages =
            [
                // a. 有館方回覆、30 字以上
                .. Recent(c => !c.IsRatingOnly && c.HasReply && c.IsLong),
                // b. 有館方回覆
                .. Recent(c => !c.IsRatingOnly && c.HasReply),
                // c. 30 字以上
                .. Recent(c => !c.IsRatingOnly && c.IsLong),
                // d. 不限時間、3 星以上：有回覆且 30 字以上 → 有回覆 → 30 字以上
                c => !c.IsRatingOnly && c.StarRating >= 3 && c.HasReply && c.IsLong,
                c => !c.IsRatingOnly && c.StarRating >= 3 && c.HasReply,
                c => !c.IsRatingOnly && c.StarRating >= 3 && c.IsLong,
                // d-4（10/16 待確認）：沒有回覆、不到 30 字的文字評論。不要的話刪掉這一行
                c => !c.IsRatingOnly && c.StarRating >= 3,
                // e. 只給星等
                .. Recent(c => c.IsRatingOnly),
                c => c.IsRatingOnly && c.StarRating >= 3,
            ];

            var picked = new List<int>(count);
            var pickedSet = new HashSet<int>();

            foreach (var stage in stages)
            {
                var stageIds = candidates.Where(c => stage(c) && !pickedSet.Contains(c.ReviewId))
                                         .Select(c => c.ReviewId)
                                         .ToList();
                int room = count - picked.Count;

                // 這一階段放得下就全收；放不下就從這一階段隨機抽 room 則
                var take = stageIds.Count <= room ? stageIds : stageIds.OrderBy(_ => random.Next()).Take(room).ToList();
                picked.AddRange(take);
                pickedSet.UnionWith(take);

                if (picked.Count >= count) break;
            }

            // Fisher–Yates 洗牌：每一種排列出現的機會一樣
            for (int i = picked.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (picked[i], picked[j]) = (picked[j], picked[i]);
            }
            return picked;
        }

        // ════════════════════════════════════════════════════════
        //  私有方法
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// 上架條件。唯一一份。
        /// 檢視表已經篩掉「不是現場評論、沒公開、被下架、周邊資料缺一項」的；這裡只加跟時間有關的兩條：
        /// <list type="bullet">
        ///   <item>還沒發生的不上架（ReviewTimeline）。也擋住「近 7 天」這類範圍只篩下限、沒篩上限的問題。</item>
        ///   <item>「已回覆」或「建立超過緩衝天數」才上架。這是刻意的：館方不作為的預設結果是公開，不能用不回覆壓著負評。</item>
        /// </list>
        /// </summary>
        private IQueryable<VReviewPublicCard> PublishedReviews(DateTime now)
        {
            DateTime cutoff = now.AddDays(-ReviewPolicy.PublicBufferDays);

            return _db.VReviewPublicCards
                      .Where(ReviewTimeline.PublicCardHappenedBy(now))
                      .Where(r => r.RepliedAt != null || r.CreatedAt <= cutoff);
        }

        /// <summary>類型、時間範圍、有無內容。星等「不」放進來——分布圖要用這個結果算。</summary>
        private static IQueryable<VReviewPublicCard> ApplyCardFilters(
            IQueryable<VReviewPublicCard> q, int? sportTypeId, string range, bool hasContentOnly, DateTime todayStart)
        {
            if (sportTypeId is int st)
                q = q.Where(r => r.SportTypeId == st);

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

        private static IQueryable<VReviewPublicCard> ApplyCardSort(IQueryable<VReviewPublicCard> q, string sort) => sort switch
        {
            ReviewSort.Newest  => q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.StarRating),
            ReviewSort.Highest => q.OrderByDescending(r => r.StarRating).ThenByDescending(r => r.CreatedAt),
            ReviewSort.Lowest  => q.OrderBy(r => r.StarRating).ThenByDescending(r => r.CreatedAt),
            _                  => q.OrderByDescending(r => r.CreatedAt)
        };
    }
}
