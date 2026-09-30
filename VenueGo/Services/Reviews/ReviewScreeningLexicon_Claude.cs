namespace VenueGo.Services.Reviews
{
    /// <summary>
    /// 評論預審「規則層」用的字詞清單。要加詞、刪詞，只改這個檔案。
    ///
    /// ── 兩個等級 ───────────────────────────────────────────────
    ///   Mask（遮蔽）：意思幾乎不會誤會的髒話、性、歧視用語。
    ///                 顧客端公開時自動換成＊，員工端也預設遮住（可按「顯示原文」）。
    ///   Flag（標記）：可能是罵人，也可能是正常用法，要看上下文。
    ///                 不遮，只在員工端用底色標出來，交給員工和 AI 判斷。
    ///
    ///   判斷原則：寧可放到 Flag，不要錯遮。
    ///   錯遮的代價是「正常評論被打上＊」，顧客會覺得館方在審查他；
    ///   放到 Flag 的代價只是員工多看一眼。
    ///
    /// ── Reason ────────────────────────────────────────────────
    ///   填 ReviewPolicy.SpamReasons 裡的文字（謾罵、猥褻、威脅、亂碼、個資、廣告、無關或不實、仇恨歧視）。
    ///   程式啟動時會檢查每個 Reason 都找得到，打錯字會直接丟例外，不會默默失效。
    ///   用文字不用索引數字：SpamReasons 哪天調整順序，這裡不必跟著改。
    ///
    /// ── 刻意「不」放進來的詞（加之前請先看這段）──────────────────
    ///   垃圾：「垃圾沒清」「垃圾桶滿了」在場館評論裡很常見，是正當的抱怨。罵人的用法交給 AI。
    ///   爛、噁心、髒：「地板很爛」「廁所很噁心」同上，是正當的負評，不能當成不雅。
    ///   機車：「機車停車位」。
    ///   靠背：「椅子的靠背」。
    ///   啥小、蝦小：「有啥小問題」＝有什麼小問題。
    ///   87、78、426 這類數字：「87 分」「426 元」誤判太多，交給 AI。
    ///   干（單字）：「干擾」「若干」。只收「干你娘」這種完整的組合。
    /// </summary>
    public static class ReviewScreeningLexicon
    {
        public enum Tier : byte { Mask = 1, Flag = 2 }

        public sealed record Entry(string Word, string Reason, Tier Tier);

        // ════════════════════════════════════════════════════════
        //  中文（含注音、emoji）
        //  比對前會先去掉空白、標點、符號，所以「幹 你 娘」「幹！你！娘」都抓得到，
        //  不必把每種寫法都列一次。
        // ════════════════════════════════════════════════════════
        public static readonly Entry[] CjkWords =
        {
            // ── 謾罵：遮蔽 ──
            new("幹你娘", "謾罵", Tier.Mask), new("幹妳娘", "謾罵", Tier.Mask), new("幹您娘", "謾罵", Tier.Mask),
            new("幹林娘", "謾罵", Tier.Mask), new("幹拎娘", "謾罵", Tier.Mask), new("幹恁娘", "謾罵", Tier.Mask),
            new("幹你老師", "謾罵", Tier.Mask), new("幹你媽", "謾罵", Tier.Mask),
            new("干你娘", "謾罵", Tier.Mask), new("干林娘", "謾罵", Tier.Mask), new("姦你娘", "謾罵", Tier.Mask),
            new("操你媽", "謾罵", Tier.Mask), new("操你娘", "謾罵", Tier.Mask), new("操你妈", "謾罵", Tier.Mask),
            new("幹", "謾罵", Tier.Mask),       // 單字：靠下面的例外詞排除「幹部」「幹嘛」等
            new("淦", "謾罵", Tier.Mask),
            new("靠北", "謾罵", Tier.Mask), new("靠杯", "謾罵", Tier.Mask), new("靠腰", "謾罵", Tier.Mask),
            new("靠夭", "謾罵", Tier.Mask), new("靠邀", "謾罵", Tier.Mask), new("靠么", "謾罵", Tier.Mask),
            new("哭爸", "謾罵", Tier.Mask), new("哭夭", "謾罵", Tier.Mask), new("哭么", "謾罵", Tier.Mask),
            new("雞掰", "謾罵", Tier.Mask), new("機掰", "謾罵", Tier.Mask), new("基掰", "謾罵", Tier.Mask),
            new("三小", "謾罵", Tier.Mask), new("殺小", "謾罵", Tier.Mask),
            new("王八蛋", "謾罵", Tier.Mask), new("王八羔子", "謾罵", Tier.Mask),
            new("混帳", "謾罵", Tier.Mask), new("混蛋", "謾罵", Tier.Mask),
            new("賤人", "謾罵", Tier.Mask), new("賤貨", "謾罵", Tier.Mask),
            new("婊子", "謾罵", Tier.Mask), new("婊", "謾罵", Tier.Mask), new("破麻", "謾罵", Tier.Mask),
            new("畜生", "謾罵", Tier.Mask), new("畜牲", "謾罵", Tier.Mask),
            new("你老母", "謾罵", Tier.Mask), new("你老木", "謾罵", Tier.Mask),
            new("你媽死了", "謾罵", Tier.Mask), new("妳媽死了", "謾罵", Tier.Mask), new("你媽死", "謾罵", Tier.Mask),
            new("傻逼", "謾罵", Tier.Mask), new("傻屄", "謾罵", Tier.Mask), new("煞筆", "謾罵", Tier.Mask),
            new("白癡", "謾罵", Tier.Mask), new("白痴", "謾罵", Tier.Mask), new("北七", "謾罵", Tier.Mask),
            new("智障", "謾罵", Tier.Mask), new("腦殘", "謾罵", Tier.Mask), new("低能", "謾罵", Tier.Mask),
            new("廢物", "謾罵", Tier.Mask), new("人渣", "謾罵", Tier.Mask), new("垃圾人", "謾罵", Tier.Mask),
            new("去死", "謾罵", Tier.Mask),
            new("ㄍㄋㄇ", "謾罵", Tier.Mask), new("ㄎㄅ", "謾罵", Tier.Mask),
            new("🖕", "謾罵", Tier.Mask),

            // ── 謾罵：只標記 ──
            new("媽的", "謾罵", Tier.Flag),      // 「我媽的包包」
            new("他媽的", "謾罵", Tier.Flag), new("你媽的", "謾罵", Tier.Flag),
            new("屁啦", "謾罵", Tier.Flag), new("放屁", "謾罵", Tier.Flag),
            new("滾蛋", "謾罵", Tier.Flag), new("閉嘴", "謾罵", Tier.Flag), new("去你的", "謾罵", Tier.Flag),
            new("神經病", "謾罵", Tier.Flag), new("豬頭", "謾罵", Tier.Flag), new("噁爛", "謾罵", Tier.Flag),
            new("沒大腦", "謾罵", Tier.Flag), new("有病", "謾罵", Tier.Flag), new("死胖子", "謾罵", Tier.Flag),
            new("尼瑪", "謾罵", Tier.Flag), new("欠揍", "威脅", Tier.Flag),

            // ── 猥褻 ──
            new("雞巴", "猥褻", Tier.Mask), new("機巴", "猥褻", Tier.Mask),
            new("肏", "猥褻", Tier.Mask), new("屌", "猥褻", Tier.Mask),
            new("懶覺", "猥褻", Tier.Mask), new("懶叫", "猥褻", Tier.Mask),
            new("約砲", "猥褻", Tier.Mask), new("約炮", "猥褻", Tier.Mask),
            new("打砲", "猥褻", Tier.Mask), new("打炮", "猥褻", Tier.Mask), new("幹砲", "猥褻", Tier.Mask),
            new("口交", "猥褻", Tier.Mask), new("援交", "猥褻", Tier.Mask),
            new("奶子", "猥褻", Tier.Flag), new("做愛", "猥褻", Tier.Flag), new("色狼", "猥褻", Tier.Flag),
            new("偷拍", "猥褻", Tier.Flag),     // 也可能是顧客在檢舉被偷拍，一定要讓員工看到

            // ── 威脅：一律只標記（意思高度依賴上下文），但會讓評論被優先處理 ──
            new("殺了你", "威脅", Tier.Flag), new("砍死", "威脅", Tier.Flag), new("打死你", "威脅", Tier.Flag),
            new("弄死你", "威脅", Tier.Flag), new("讓你好看", "威脅", Tier.Flag), new("等著瞧", "威脅", Tier.Flag),
            new("走著瞧", "威脅", Tier.Flag), new("給我小心", "威脅", Tier.Flag), new("堵你", "威脅", Tier.Flag),
            new("找人處理", "威脅", Tier.Flag), new("放火", "威脅", Tier.Flag), new("炸掉", "威脅", Tier.Flag),

            // ── 仇恨歧視 ──
            new("支那", "仇恨歧視", Tier.Mask), new("番仔", "仇恨歧視", Tier.Mask),
            new("黑鬼", "仇恨歧視", Tier.Mask), new("娘炮", "仇恨歧視", Tier.Mask), new("人妖", "仇恨歧視", Tier.Mask),
            new("死同性戀", "仇恨歧視", Tier.Mask), new("殘廢", "仇恨歧視", Tier.Mask),
            new("外勞", "仇恨歧視", Tier.Flag),  // 不一定是歧視用法，但常出現在歧視語境

            // ── 廣告：只標記 ──
            new("加賴", "廣告", Tier.Flag), new("加line", "廣告", Tier.Flag), new("私訊", "廣告", Tier.Flag),
            new("優惠碼", "廣告", Tier.Flag), new("折扣碼", "廣告", Tier.Flag), new("推薦碼", "廣告", Tier.Flag),
            new("團購", "廣告", Tier.Flag), new("代購", "廣告", Tier.Flag), new("代操", "廣告", Tier.Flag),
            new("投資群組", "廣告", Tier.Flag), new("穩賺", "廣告", Tier.Flag), new("兼職", "廣告", Tier.Flag),
            new("點我", "廣告", Tier.Flag), new("限時優惠", "廣告", Tier.Flag), new("官方帳號", "廣告", Tier.Flag),
        };

        // ════════════════════════════════════════════════════════
        //  例外詞：一個命中如果「整個落在」某個例外詞裡面，就不算。
        //  例：「幹部」裡的「幹」不算；但「想幹你娘」的「幹你娘」沒有整個落在「想幹」裡，照樣算。
        //  例外詞一樣會先去掉空白標點再比對。
        // ════════════════════════════════════════════════════════
        public static readonly string[] Exceptions =
        {
            // 幹
            "幹部", "幹事", "幹練", "幹勁", "幹線", "幹道", "幹員", "能幹", "才幹", "精幹", "苦幹", "實幹",
            "樹幹", "主幹", "骨幹", "軀幹", "枝幹", "幹活", "幹嘛", "幹麻", "幹啥", "幹什麼", "幹甚麼",
            "幹得好", "幹細胞",
            // 三小
            "三小時", "三小孩", "三小隊", "三小節", "三小碗", "三小盤", "三小杯", "三小包",
            // 靠北（方位）
            "靠北邊", "靠北側", "靠北面", "靠北方", "靠北門",
            // 低能
            "低能見度", "低能量", "低能耗",
            // 廢物
            "廢物利用", "廢物回收",
            // 去死
            "去死角", "去死皮",
            // 番仔
            "番仔火",
        };

        // ════════════════════════════════════════════════════════
        //  英文：用「單字邊界」比對，字母之間允許夾 0～2 個符號（f*ck、f.u.c.k）。
        //  ⚠️ 英文不能像中文那樣先把空白去掉：「is hit」去掉空白會變成「ishit」，
        //     裡面就有 shit。所以英文另外用正規表示式、並要求前後不是英文字母。
        //  Pattern 是「每個字母一個位置」，[ux*@] 表示這個位置可以是 u、x、*、@ 任一個。
        // ════════════════════════════════════════════════════════
        public sealed record AsciiEntry(string[] Letters, string Reason, Tier Tier);

        public static readonly AsciiEntry[] AsciiWords =
        {
            new(new[] { "f", "[ux*@]", "c", "k" }, "謾罵", Tier.Mask),
            new(new[] { "f", "k" }, "謾罵", Tier.Mask),
            new(new[] { "s", "h", "[i1!*]", "t" }, "謾罵", Tier.Mask),
            new(new[] { "b", "[i1!*]", "t", "c", "h" }, "謾罵", Tier.Mask),
            new(new[] { "g", "8" }, "謾罵", Tier.Mask),           // 機掰的縮寫
            new(new[] { "w", "t", "f" }, "謾罵", Tier.Flag),
            new(new[] { "s", "t", "f", "u" }, "謾罵", Tier.Flag),
            new(new[] { "d", "a", "m", "n" }, "謾罵", Tier.Flag),
            new(new[] { "g", "a", "n" }, "謾罵", Tier.Flag),      // 「幹」的台語拼音；也可能是人名的一部分，所以只標記
            new(new[] { "n", "m", "s", "l" }, "謾罵", Tier.Mask), // 你媽死了
            new(new[] { "c", "n", "m" }, "謾罵", Tier.Mask),      // 操你媽
            new(new[] { "t", "m", "d" }, "謾罵", Tier.Flag),      // 他媽的
            new(new[] { "n", "m", "d" }, "謾罵", Tier.Flag),      // 你媽的
        };

        // ════════════════════════════════════════════════════════
        //  本站網址：這些網域的連結「不」遮蔽、也不算廣告。
        //  顧客貼本站的頁面，通常是在說「這一頁的場地介紹寫錯了」，員工需要看到是哪一頁。
        //  ⚠️ 正式上線的網域確定之後，加在這裡（只寫主網域，子網域會自動算進去）。
        // ════════════════════════════════════════════════════════
        public static readonly string[] OwnSiteHosts =
        {
            "localhost",
            // "venuego.com.tw",
        };

        // ════════════════════════════════════════════════════════
        //  句型規則：針對「人」的外貌、身體、性暗示評論。
        //
        //  單字清單做不到這件事：「很黑」「很白」「很正」本身是正常的字，
        //  「場地很黑」是燈光的抱怨，「櫃台小姐很黑」才是在評論人的外貌。
        //  所以這裡要求「前面緊接著一個指人的詞」（最多隔 2 個字），才算數。
        //
        //  寫法是正規表示式；(?<m>⋯) 是要遮蔽或標記的部分，沒寫的話整段都算。
        //  只遮外貌的那幾個字、保留「櫃台小姐」，讀的人才知道原本是在說誰，而不是整句話消失。
        //
        //  理由（9/27）：針對人的四條用「騷擾或不當言論」（ReviewPolicy.SpamReasons 第 8 項）；
        //  「欠幹」不一定是針對人（「欠幹的服務」），維持「猥褻」。
        //
        //  ⚠️ 這一層只能抓「長得像」的寫法，換句話說就抓不到（「她今天穿得很清涼」）。
        //     那些交給 AI 層。
        // ════════════════════════════════════════════════════════
        public sealed record PatternRule(string Pattern, string Reason, Tier Tier, string Note);

        // 指人的詞。「他」「她」前面不能是「其」（其他），後面不能是「們」：
        // 「他們場地很黑」是在說場地，不是在說人。
        private const string Person =
            @"(?:櫃台|櫃檯|小姐|先生|妹妹|妹子|美女|帥哥|女生|男生|女的|男的|教練|員工|店員|工讀生|服務員|服務生|阿姨|大叔|大嬸|老闆娘|老闆|那個人|那位|顧客|客人|球友|(?<!其)[她他](?!們))";

        // 程度副詞：「很辣」「超正」。刻意要求要有副詞，單獨一個「正」「白」太容易撞到「正在」「白天」。
        private const string Very = @"(?:很|好|超|蠻|真|有夠|太|挺)";

        // 後面接這些字就不是在評論外貌：很正常、很正確、很白目、很黑心、很胖胖的?（不排除）
        private const string NotAppearance = @"(?![常確式直派經在要好巧當是目爛心暗痴癡])";

        public static readonly PatternRule[] PatternRules =
        {
            new($@"{Person}[^。！？!?\n，,；;、]{{0,2}}?(?<m>{Very}(?:辣|正|騷|性感){NotAppearance}|正妹|騷貨)",
                "騷擾或不當言論", Tier.Mask, "對人的性化評論：櫃台小姐很辣、那個教練超正"),

            new($@"{Person}[^。！？!?\n，,；;、]{{0,2}}?(?<m>{Very}(?:醜|黑|白|胖|肥|矮|禿){NotAppearance}|醜八怪|肥豬|沒化妝|妝太濃|妝很濃|素顏)",
                "騷擾或不當言論", Tier.Mask, "對人的外貌評論：工讀生很胖、她沒化妝"),

            new($@"(?<m>(?:胸部?|(?<![牛豆鮮優煉羊椰杏])奶|屁股|臀部?){Very}?(?:大|翹|挺|美|讚|性感|好看|正))",
                "騷擾或不當言論", Tier.Mask, "身體部位＋評價：胸很大、屁股很翹"),

            new(@"(?<m>欠(?:幹|操|肏|姦))",
                "猥褻", Tier.Mask, "欠幹、欠操"),

            new(@"(?<m>(?:想不想|要不要)?(?:來|去)我家|約嗎|約不約|單身嗎|有沒有(?:男|女)(?:朋友|友)|有(?:男|女)(?:朋友|友)嗎|(?:電話|手機|賴|line|ig)給我)",
                "騷擾或不當言論", Tier.Flag, "搭訕：想不想來我家、單身嗎、電話給我"),
        };
    }
}
