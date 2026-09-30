using System.Text;
using System.Text.RegularExpressions;
using VenueGo.Helpers;
using static VenueGo.Services.Reviews.ReviewScreeningLexicon;

namespace VenueGo.Services.Reviews
{
    // ════════════════════════════════════════════════════════════
    //  評論預審：規則層
    //
    //  負責「長相固定、用規則就抓得到」的東西：個資、連結、字詞清單裡的髒話。
    //  需要看懂語意的（疑似推銷、離題、情緒強度、主題、摘要）交給 AI 層，不在這裡。
    //
    //  ── 為什麼要有規則層，不全部交給 AI ─────────────────────────
    //  1. 騙不了：評論可以寫「本評論沒有不雅字眼」來誘導 AI，但騙不了正規表示式。
    //  2. 一定有結果：AI 會逾時、會掛、會回傳格式錯的東西；規則層不會。
    //     所以「公開時遮蔽個資」只能靠這一層，不能等 AI。
    //  3. 免費、瞬間完成：顧客送出評論的當下就能算，不用等背景工作。
    //
    //  ── 這一層「不」做的事 ─────────────────────────────────────
    //  不寫資料庫、不呼叫任何服務，純粹「文字進、結果出」。
    //  好處是可以直接寫測試，也可以在任何地方呼叫（顧客端 API、館方清單、背景工作）。
    // ════════════════════════════════════════════════════════════

    public enum HitKind : byte
    {
        Mobile = 1, Landline = 2, Email = 3, NationalId = 4, CardNumber = 5, LineId = 6, Url = 7, Word = 8
    }

    /// <summary>一個命中。Start、Length 是「原文」裡的位置。</summary>
    public sealed record TextHit(int Start, int Length, HitKind Kind, string Reason, Tier Tier);

    public enum SegmentKind : byte
    {
        /// <summary>一般文字</summary>
        Plain = 0,
        /// <summary>已遮蔽：Text 是替代文字（〔電話已隱藏〕、＊＊＊），Original 是原文</summary>
        Masked = 1,
        /// <summary>只標記、沒有遮：Text 就是原文，員工端用底色標出來</summary>
        Flagged = 2
    }

    /// <summary>
    /// 切好的一段文字。員工端用它來畫畫面：一段一段輸出，Razor 會自動跳脫，
    /// 不必自己組 HTML（顧客寫的內容不能用 Html.Raw）。
    /// </summary>
    public sealed record TextSegment(string Text, SegmentKind Kind, string? Original, string? Reason);

    public sealed class ScreenResult
    {
        public static readonly ScreenResult Empty = new(Array.Empty<TextHit>(), string.Empty, Array.Empty<TextSegment>());

        public ScreenResult(IReadOnlyList<TextHit> hits, string publicText, IReadOnlyList<TextSegment> segments)
        {
            Hits = hits;
            PublicText = publicText;
            Segments = segments;
        }

        public IReadOnlyList<TextHit> Hits { get; }

        /// <summary>顧客端公開時顯示的文字：個資、連結、遮蔽級字詞都已換掉。</summary>
        public string PublicText { get; }

        /// <summary>員工端逐段顯示用。</summary>
        public IReadOnlyList<TextSegment> Segments { get; }

        public bool HasPii => Hits.Any(h => h.Reason == "個資");

        /// <summary>
        /// 建議的垃圾標記理由（ReviewPolicy.SpamReasons 的索引），不重複、由小到大。
        /// 只是「建議」：員工按確認才會真的標記。
        /// </summary>
        public IReadOnlyList<byte> SuggestedReasons =>
            Hits.Select(h => (byte)Array.IndexOf(ReviewPolicy.SpamReasons, h.Reason))
                .Distinct().OrderBy(b => b).ToList();

        /// <summary>
        /// 要不要優先處理（未讀清單上方的提示條、讀過後自動置頂）。
        /// 個資以外的任何命中都算：
        ///   遮蔽級——雖然公開時已經遮了，但員工仍要決定要不要整則標記為垃圾；
        ///   標記級——公開時「不會」自動遮，更需要員工在上架前看過。
        /// 個資不算：它已經自動遮蔽，而且顧客寫出自己的電話通常不是惡意，不需要搶時間處理。
        /// </summary>
        public bool IsPriority => Hits.Any(h => h.Reason != "個資");
    }

    public static class ReviewTextGuard
    {
        // ── 啟動時檢查字詞清單的 Reason 都寫對 ──
        //  打錯字（例如「謾駡」）的話，這個詞會變成「找不到理由」，
        //  與其默默失效，不如程式一跑就炸，讓人馬上發現。
        static ReviewTextGuard()
        {
            var reasons = CjkWords.Select(w => w.Reason)
                                  .Concat(AsciiWords.Select(w => w.Reason))
                                  .Concat(PatternRules.Select(p => p.Reason));
            foreach (var r in reasons.Distinct())
                if (Array.IndexOf(ReviewPolicy.SpamReasons, r) < 0)
                    throw new InvalidOperationException($"字詞清單的理由「{r}」不在 ReviewPolicy.SpamReasons 裡");

            // 長的詞先比：「幹你娘」要整個算一個，不能先被「幹」吃掉一個字
            SortedCjk = CjkWords.Select(w => w with { Word = Normalize(w.Word).Text })
                                .OrderByDescending(w => w.Word.Length).ToArray();
            NormalizedExceptions = Exceptions.Select(e => Normalize(e).Text).ToArray();
            AsciiRegexes = AsciiWords.Select(w => (BuildAsciiRegex(w.Letters), w.Reason, w.Tier)).ToArray();
            CompiledPatterns = PatternRules.Select(p => (new Regex(p.Pattern, Opt), p.Reason, p.Tier)).ToArray();
        }

        private static readonly Entry[] SortedCjk;
        private static readonly string[] NormalizedExceptions;
        private static readonly (Regex Regex, string Reason, Tier Tier)[] AsciiRegexes;
        private static readonly (Regex Regex, string Reason, Tier Tier)[] CompiledPatterns;

        // ════════════════════════════════════════════════════════
        //  個資與連結的樣式
        //  比對前會先把全形英數字換成半形（０９１２ → 0912），長度不變，位置可以直接對回原文。
        // ════════════════════════════════════════════════════════
        private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // Email
        private static readonly Regex EmailRx = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}", Opt);

        // 網址：有 http(s):// 或 www. 開頭的，以及常見網域結尾的（example.com.tw、lin.ee/xxx）
        private static readonly Regex UrlRx = new(
            @"(?:https?://|www\.)[^\s，。！？、）)」]+"
          + @"|(?<![A-Za-z0-9@.-])(?:[A-Za-z0-9-]+\.)+(?:com|net|org|tw|cc|me|io|co|ee|shop|xyz|top|info|app)(?:\.[A-Za-z]{2})?(?:/[^\s，。！？、）)」]*)?(?![A-Za-z0-9])",
            Opt);

        // 信用卡：13～19 碼，可以夾空白或 -；還要通過 Luhn 檢查碼才算（擋掉一般長數字）
        // 前面不能緊貼英文字母：訂單編號是「VG＋日期＋流水號」，數字部分可能剛好 13 碼以上，
        // 萬一又剛好通過 Luhn（機率約十分之一），顧客寫的訂單編號會被遮掉，員工就查不到訂單了。
        private static readonly Regex CardRx = new(@"(?<![A-Za-z0-9])\d(?:[ -]?\d){12,18}(?!\d)", Opt);

        // 身分證字號、新式居留證：英文字母＋1/2/8/9＋8 碼。
        // 不看檢查碼（9/27 改）：打錯一碼的身分證仍然幾乎能認出是誰，而且評論裡很少有別的東西長這樣。
        private static readonly Regex NationalIdRx = new(@"(?<![A-Za-z0-9])[A-Za-z][1289]\d{8}(?!\d)", Opt);

        // 手機：0912-345-678、0912 345 678、+886 912345678
        private static readonly Regex MobileRx = new(@"(?<!\d)(?:\+?886[-\s]?|0)9\d{2}[-\s.]?\d{3}[-\s.]?\d{3}(?!\d)", Opt);

        // 市話：02-2345-6789、(07)123-4567、049-2123456
        private static readonly Regex LandlineRx = new(@"(?<!\d)(?:\(0\d{1,2}\)|0[2-8]\d?)[-\s.]?\d{3,4}[-\s.]?\d{4}(?!\d)", Opt);

        // LINE ID：一定要有明確的前綴（line id、line：、加line、賴：），否則「line time」這種英文也會中。
        // 只遮 ID 本身（第 1 組），保留「加LINE：」讓讀的人知道這裡原本寫了什麼。
        private static readonly Regex LineIdRx = new(
            @"(?:(?<![A-Za-z])line\s*id\s*[:：]?\s*|(?<![A-Za-z])line\s*[:：]\s*|加\s*(?:line|賴)\s*[:：]?\s*|賴\s*[:：]\s*)(@?[A-Za-z0-9._-]{4,20})(?![A-Za-z0-9._-])",
            Opt);

        private static string MaskTokenFor(HitKind kind) => kind switch
        {
            HitKind.Mobile     => "〔電話已隱藏〕",
            HitKind.Landline   => "〔電話已隱藏〕",
            HitKind.Email      => "〔Email 已隱藏〕",
            HitKind.NationalId => "〔證號已隱藏〕",
            HitKind.CardNumber => "〔卡號已隱藏〕",
            HitKind.LineId     => "〔LINE ID 已隱藏〕",
            HitKind.Url        => "〔連結已隱藏〕",
            HitKind.Word       => throw new InvalidOperationException("字詞的遮蔽由 MaskWord 處理"),
            _                  => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未定義的命中種類")
        };

        // ════════════════════════════════════════════════════════
        //  對外的入口
        // ════════════════════════════════════════════════════════

        /// <summary>顧客端公開時用：只要遮好的文字。</summary>
        public static string MaskForPublic(string? text) => Screen(text).PublicText;

        public static ScreenResult Screen(string? text)
        {
            if (string.IsNullOrEmpty(text)) return ScreenResult.Empty;

            string wide = ToHalfWidth(text);
            var hits = new List<TextHit>();
            var claimed = new bool[text.Length];   // 已經被個資或連結吃掉的位置，後面的比對不再看

            // ── 1. 個資與連結（順序有意義：Email 要在網址前面，否則 a@b.com 的 b.com 會先被當成網址）──
            AddRegexHits(wide, EmailRx,    HitKind.Email,      "個資", hits, claimed);
            AddUrlHits(wide, hits, claimed);
            AddRegexHits(wide, CardRx,     HitKind.CardNumber, "個資", hits, claimed, m => PassesLuhn(m.Value));
            AddRegexHits(wide, NationalIdRx, HitKind.NationalId, "個資", hits, claimed);
            AddRegexHits(wide, MobileRx,   HitKind.Mobile,     "個資", hits, claimed);
            AddRegexHits(wide, LandlineRx, HitKind.Landline,   "個資", hits, claimed);
            AddRegexHits(wide, LineIdRx,   HitKind.LineId,     "個資", hits, claimed, group: 1);

            // ── 2. 句型規則（對人的外貌、身體、性暗示評論）──
            foreach (var (rx, reason, tier) in CompiledPatterns)
                foreach (Match m in rx.Matches(wide))
                {
                    var g = m.Groups["m"].Success ? m.Groups["m"] : m.Groups[0];
                    if (Overlaps(claimed, g.Index, g.Length)) continue;
                    hits.Add(new TextHit(g.Index, g.Length, HitKind.Word, reason, tier));
                    Claim(claimed, g.Index, g.Length);
                }

            // ── 3. 英文字詞 ──
            foreach (var (rx, reason, tier) in AsciiRegexes)
                foreach (Match m in rx.Matches(wide))
                    if (!Overlaps(claimed, m.Index, m.Length))
                    {
                        hits.Add(new TextHit(m.Index, m.Length, HitKind.Word, reason, tier));
                        Claim(claimed, m.Index, m.Length);
                    }

            // ── 4. 中文字詞 ──
            AddCjkHits(text, wide, hits, claimed);

            hits.Sort((a, b) => a.Start.CompareTo(b.Start));
            return Build(text, hits);
        }

        // ════════════════════════════════════════════════════════
        //  中文字詞比對
        // ════════════════════════════════════════════════════════

        private static void AddCjkHits(string text, string wide, List<TextHit> hits, bool[] claimed)
        {
            var (norm, map) = Normalize(wide, claimed);

            // 例外詞出現的區間
            var protectedRanges = new List<(int Start, int End)>();
            foreach (var ex in NormalizedExceptions)
                for (int i = norm.IndexOf(ex, StringComparison.Ordinal); i >= 0; i = norm.IndexOf(ex, i + 1, StringComparison.Ordinal))
                    protectedRanges.Add((i, i + ex.Length));

            var used = new bool[norm.Length];
            foreach (var w in SortedCjk)
            {
                for (int i = norm.IndexOf(w.Word, StringComparison.Ordinal); i >= 0; i = norm.IndexOf(w.Word, i + 1, StringComparison.Ordinal))
                {
                    int end = i + w.Word.Length;

                    // 整個落在某個例外詞裡面 → 不算（「幹部」的「幹」）
                    if (protectedRanges.Any(p => p.Start <= i && end <= p.End)) continue;
                    // 已經被更長的詞算過 → 不重複算（「幹你娘」裡的「幹」）
                    if (Enumerable.Range(i, w.Word.Length).Any(k => used[k])) continue;

                    for (int k = i; k < end; k++) used[k] = true;

                    // 對回原文的位置：中間夾的空白、標點一起算進去（「幹 你 娘」整段遮掉）
                    int start = map[i];
                    int stop = map[end - 1] + 1;
                    hits.Add(new TextHit(start, stop - start, HitKind.Word, w.Reason, w.Tier));
                }
            }
        }

        /// <summary>
        /// 正規化：去掉空白、標點、符號、零寬字元，英文轉小寫。
        /// map[i] 記錄「正規化後第 i 個字」是原文的第幾個字，遮蔽時才能對回原文。
        /// 被個資吃掉的位置換成一個斷點字元，讓前後的字不會接起來被誤判。
        /// </summary>
        private static (string Text, int[] Map) Normalize(string s, bool[]? claimed = null)
        {
            const char Break = '\u0001';
            var sb = new StringBuilder(s.Length);
            var map = new List<int>(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (claimed != null && claimed[i])
                {
                    if (sb.Length == 0 || sb[^1] != Break) { sb.Append(Break); map.Add(i); }
                    continue;
                }
                char c = s[i];
                if (char.IsSurrogate(c)) { sb.Append(c); map.Add(i); continue; }   // emoji（🖕）要保留
                if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c) || char.IsControl(c)) continue;
                if (c is '​' or '‌' or '‍' or '﻿') continue;           // 零寬字元：有人用它來拆字躲過濾
                if (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format) continue;
                sb.Append(char.ToLowerInvariant(c));
                map.Add(i);
            }
            return (sb.ToString(), map.ToArray());
        }

        // ════════════════════════════════════════════════════════
        //  組出結果
        // ════════════════════════════════════════════════════════

        private static ScreenResult Build(string text, List<TextHit> hits)
        {
            var pub = new StringBuilder(text.Length);
            var segments = new List<TextSegment>();
            int cursor = 0;

            foreach (var h in hits)
            {
                if (h.Start < cursor) continue;   // 理論上不會重疊；萬一重疊，以先出現的為準

                if (h.Start > cursor)
                {
                    string plain = text[cursor..h.Start];
                    pub.Append(plain);
                    segments.Add(new TextSegment(plain, SegmentKind.Plain, null, null));
                }

                string original = text.Substring(h.Start, h.Length);
                switch (h.Tier)
                {
                    case Tier.Mask:
                        {
                            string token = h.Kind == HitKind.Word ? MaskWord(original) : MaskTokenFor(h.Kind);
                            pub.Append(token);
                            segments.Add(new TextSegment(token, SegmentKind.Masked, original, h.Reason));
                            break;
                        }
                    case Tier.Flag:
                        pub.Append(original);
                        segments.Add(new TextSegment(original, SegmentKind.Flagged, null, h.Reason));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(hits), h.Tier, "未定義的等級");
                }
                cursor = h.Start + h.Length;
            }

            if (cursor < text.Length)
            {
                string rest = text[cursor..];
                pub.Append(rest);
                segments.Add(new TextSegment(rest, SegmentKind.Plain, null, null));
            }

            return new ScreenResult(hits, pub.ToString(), segments);
        }

        /// <summary>
        /// 字詞換成＊：空白保留，其他每個字一顆星（emoji 算一個字）。
        /// 夾在中間的符號也換成星：「f*ck」的 * 本來就是在頂替字母。
        /// 零寬字元直接拿掉：留著的話，複製出去還是能還原成原本的字。
        /// </summary>
        private static string MaskWord(string original)
        {
            var sb = new StringBuilder(original.Length);
            foreach (char c in original)
            {
                if (char.IsLowSurrogate(c)) continue;
                if (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format) continue;
                sb.Append(char.IsWhiteSpace(c) ? c : '＊');
            }
            return sb.ToString();
        }

        // ════════════════════════════════════════════════════════
        //  小工具
        // ════════════════════════════════════════════════════════

        private static void AddRegexHits(string wide, Regex rx, HitKind kind, string reason,
                                         List<TextHit> hits, bool[] claimed,
                                         Func<Match, bool>? accept = null, int group = 0)
        {
            foreach (Match m in rx.Matches(wide))
            {
                var g = m.Groups[group];
                if (!g.Success) continue;
                if (accept != null && !accept(m)) continue;
                if (Overlaps(claimed, g.Index, g.Length)) continue;
                hits.Add(new TextHit(g.Index, g.Length, kind, reason, Tier.Mask));
                Claim(claimed, g.Index, g.Length);
            }
        }

        /// <summary>
        /// 網址：本站的連結只「占位」（後面的字詞比對不再看它），不算命中、不遮蔽；
        /// 其他網址遮蔽並記為廣告。
        /// </summary>
        private static void AddUrlHits(string wide, List<TextHit> hits, bool[] claimed)
        {
            foreach (Match m in UrlRx.Matches(wide))
            {
                if (Overlaps(claimed, m.Index, m.Length)) continue;
                if (!IsOwnSite(m.Value))
                    hits.Add(new TextHit(m.Index, m.Length, HitKind.Url, "廣告", Tier.Mask));
                Claim(claimed, m.Index, m.Length);
            }
        }

        private static bool IsOwnSite(string url)
        {
            string host = Regex.Replace(url, @"^(?:https?://)?(?:www\.)?", "", RegexOptions.IgnoreCase);
            int cut = host.IndexOfAny(new[] { '/', ':', '?', '#' });
            if (cut >= 0) host = host[..cut];
            host = host.ToLowerInvariant();
            return OwnSiteHosts.Any(own => host == own || host.EndsWith("." + own, StringComparison.Ordinal));
        }

        private static bool Overlaps(bool[] claimed, int start, int length)
        {
            for (int i = start; i < start + length; i++) if (claimed[i]) return true;
            return false;
        }

        private static void Claim(bool[] claimed, int start, int length)
        {
            for (int i = start; i < start + length; i++) claimed[i] = true;
        }

        /// <summary>全形英數字與符號 → 半形。一個字換一個字，長度不變。</summary>
        private static string ToHalfWidth(string s)
        {
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c >= '！' && c <= '～') chars[i] = (char)(c - 0xFEE0);
                else if (c == '　') chars[i] = ' ';
            }
            return new string(chars);
        }

        /// <summary>英文字詞：前後不能緊貼英文或數字；字母之間可以夾 0～2 個非英數字元。</summary>
        private static Regex BuildAsciiRegex(string[] letters)
        {
            string body = string.Join(@"[^A-Za-z0-9]{0,2}", letters);
            return new Regex($@"(?<![A-Za-z0-9]){body}(?![A-Za-z0-9])", Opt);
        }

        /// <summary>信用卡號的 Luhn 檢查碼</summary>
        private static bool PassesLuhn(string raw)
        {
            var digits = raw.Where(char.IsDigit).Select(c => c - '0').ToArray();
            if (digits.Length < 13 || digits.Length > 19) return false;
            int sum = 0;
            for (int i = 0; i < digits.Length; i++)
            {
                int d = digits[digits.Length - 1 - i];
                if (i % 2 == 1) { d *= 2; if (d > 9) d -= 9; }
                sum += d;
            }
            return sum % 10 == 0;
        }
    }
}
