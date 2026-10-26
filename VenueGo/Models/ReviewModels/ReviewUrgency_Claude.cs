using VenueGo.Models.Entities;
using VenueGo.ViewModels.ReviewViewModels;

namespace VenueGo.Models.ReviewModels
{
    /// <summary>
    /// 負評優先（10/26）：一則還沒處理的評論，現在該做什麼（閱覽或回覆）、期限是什麼時候、要不要提醒。
    /// <para>
    /// 【期限表】評分越低期限越短。「目標」是平常該做到的，「最遲」是遇到連續放假時的底線。
    /// <code>
    ///   星等    閱覽（目標／最遲）       處理完（目標／最遲）
    ///   1–2★   當天／3 天              3 天／7 天
    ///   3★     2 天／3 天              5 天／7 天
    ///   4–5★   3 天／—                 7 天
    /// </code>
    /// 處理完的「最遲 7 天」就是 ReviewPolicy.PublicBufferDays：滿 7 天沒回覆會自動公開，
    /// 所以「未處理完的負評不能公開」等於最遲期限設在公開那一刻。
    /// </para>
    /// <para>
    /// 【負評的「當天」】原則：員工上班期間看得到的負評，下班前一定要讀完。
    /// 評論大多在晚上寫（剛打完球），員工隔天上班才看得到，所以：
    ///   評論在 StaffDayEnd 前 LateArrivalGrace（17:00）以前進來 → 當天 StaffDayEnd（18:00）前要讀；
    ///   之後才進來的 → 隔天 18:00 前要讀。
    /// 留 1 小時緩衝，是因為 17:59 進來的評論不該只給 1 分鐘。
    /// </para>
    /// <para>
    /// 【提醒的顏色】跟原本一樣用 OverdueLevel：
    ///   Overdue（紅）：滿 7 天還沒處理（已經自動公開了）。跟原本的意思一樣，IsAutoPublished 靠它判斷。
    ///   Late（紅）：還沒閱覽，而且超過閱覽的「最遲」期限。
    ///   Soon（橘）：到了期限的那一天、或已經過了目標期限；另外放了滿 ReviewPolicy.OverdueWarnDays（3 天）一律提醒，
    ///              跟原本一樣，好評也不會放超過 3 天都沒有提醒。
    /// </para>
    /// <para>
    /// 只看評論本身的欄位和傳進來的「現在」，不碰資料庫，可以直接寫單元測試。
    /// </para>
    /// </summary>
    public static class ReviewUrgency
    {
        /// <summary>負評「當天」的期限時間：員工下班時間（10/26 先訂 18:00）。</summary>
        public static readonly TimeSpan StaffDayEnd = new(18, 0, 0);

        /// <summary>下班前多久以內進來的負評，算到隔天（17:59 進來的不該只給 1 分鐘）。</summary>
        public static readonly TimeSpan LateArrivalGrace = TimeSpan.FromHours(1);

        /// <summary>閱覽的最遲期限（1–3★）。4–5★ 沒有閱覽的最遲期限，只看處理完的期限。</summary>
        public const int ReadLimitDays = 3;

        /// <summary>星等分三級。</summary>
        public enum Tier
        {
            Negative,   // 1–2★
            Neutral,    // 3★
            Positive    // 4–5★
        }

        public static Tier TierOf(byte starRating) => starRating switch
        {
            1 or 2 => Tier.Negative,
            3      => Tier.Neutral,
            4 or 5 => Tier.Positive,
            _ => throw new ArgumentOutOfRangeException(nameof(starRating), starRating, "星等只有 1～5")
        };

        /// <summary>現在該做什麼。已回覆或已標垃圾的不用做。</summary>
        public static DeadlineStage StageOf(Review r)
        {
            bool handled = r.RepliedAt != null || r.SpamMarkedAt != null;
            if (handled) return DeadlineStage.None;
            if (!handled && r.ReadAt == null) return DeadlineStage.Read;
            if (!handled && r.ReadAt != null) return DeadlineStage.Reply;
            throw new InvalidOperationException("不會走到這裡：上面三種情況已經涵蓋全部");
        }

        /// <summary>閱覽的目標期限。</summary>
        public static DateTime ReadDue(DateTime createdAt, byte starRating) => TierOf(starRating) switch
        {
            Tier.Negative => SameDayDue(createdAt),
            Tier.Neutral  => createdAt.AddDays(2),
            Tier.Positive => createdAt.AddDays(3),
            _ => throw new ArgumentOutOfRangeException(nameof(starRating))
        };

        /// <summary>閱覽的最遲期限。4–5★ 沒有（null）。</summary>
        public static DateTime? ReadLimit(DateTime createdAt, byte starRating) => TierOf(starRating) switch
        {
            Tier.Negative => createdAt.AddDays(ReadLimitDays),
            Tier.Neutral  => createdAt.AddDays(ReadLimitDays),
            Tier.Positive => null,
            _ => throw new ArgumentOutOfRangeException(nameof(starRating))
        };

        /// <summary>處理完（回覆或標垃圾）的目標期限。</summary>
        public static DateTime ReplyDue(DateTime createdAt, byte starRating) => TierOf(starRating) switch
        {
            Tier.Negative => createdAt.AddDays(3),
            Tier.Neutral  => createdAt.AddDays(5),
            Tier.Positive => createdAt.AddDays(ReviewPolicy.PublicBufferDays),
            _ => throw new ArgumentOutOfRangeException(nameof(starRating))
        };

        /// <summary>負評的「當天」（檔頭）。</summary>
        public static DateTime SameDayDue(DateTime createdAt)
        {
            bool beforeCutoff = createdAt.TimeOfDay < StaffDayEnd - LateArrivalGrace;
            if (beforeCutoff) return createdAt.Date + StaffDayEnd;
            if (!beforeCutoff) return createdAt.Date.AddDays(1) + StaffDayEnd;
            throw new InvalidOperationException("不會走到這裡");
        }

        /// <summary>目前這個階段的目標期限。不用處理的是 null。</summary>
        public static DateTime? DueOf(Review r) => StageOf(r) switch
        {
            DeadlineStage.Read  => ReadDue(r.CreatedAt, r.StarRating),
            DeadlineStage.Reply => ReplyDue(r.CreatedAt, r.StarRating),
            DeadlineStage.None  => null,
            _ => throw new ArgumentOutOfRangeException(nameof(r))
        };

        /// <summary>提醒的顏色（檔頭）。</summary>
        public static OverdueLevel LevelOf(Review r, DateTime now)
        {
            var stage = StageOf(r);
            if (stage == DeadlineStage.None) return OverdueLevel.None;

            // 1. 滿 7 天沒處理：已經自動公開（跟原本的 GetOverdueLevel 一樣）
            if (now - r.CreatedAt >= TimeSpan.FromDays(ReviewPolicy.PublicBufferDays)) return OverdueLevel.Overdue;

            // 2. 還沒閱覽，超過閱覽的最遲期限
            if (stage == DeadlineStage.Read && ReadLimit(r.CreatedAt, r.StarRating) is DateTime limit && now >= limit)
                return OverdueLevel.Late;

            // 3. 期限當天、或已經過了目標期限；放了滿 3 天也一律提醒（原本的規則）
            DateTime due = DueOf(r)!.Value;
            bool dueTodayOrPassed = now.Date >= due.Date;
            bool waitedLong = now - r.CreatedAt >= TimeSpan.FromDays(ReviewPolicy.OverdueWarnDays);
            if (dueTodayOrPassed || waitedLong) return OverdueLevel.Soon;
            if (!dueTodayOrPassed && !waitedLong) return OverdueLevel.None;
            throw new InvalidOperationException("不會走到這裡");
        }

        /// <summary>
        /// 期限的文字：「今天 18:00」「明天 18:00」「10/29 20:15」。
        /// 年份不同時（跨年）才加年份。
        /// </summary>
        public static string DueText(DateTime due, DateTime now)
        {
            string time = due.ToString("HH:mm");
            if (due.Date == now.Date) return $"今天 {time}";
            if (due.Date == now.Date.AddDays(1)) return $"明天 {time}";
            if (due.Year == now.Year) return $"{due.Month}/{due.Day} {time}";
            if (due.Year != now.Year) return $"{due.Year}/{due.Month}/{due.Day} {time}";
            throw new InvalidOperationException("不會走到這裡");
        }
    }
}
