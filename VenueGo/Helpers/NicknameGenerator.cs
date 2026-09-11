// ============================================================
//  匿名暱稱產生器（骨架）
//
//  放置路徑：Helpers/NicknameGenerator.cs
//  命名空間依你專案實際的根命名空間調整。
//
//  ── 為什麼放 Helpers/ 而不是 Services/ ──────────────────
//  它不需要資料庫、不需要注入任何東西，同樣的輸入永遠得到
//  同樣形狀的輸出（只差隨機）。這種東西做成 static 就好，
//  不必為了「看起來像 Service」而包一層介面再註冊到 DI。
//
//  ⚠️ 什麼時候才該包成介面：當你想在測試裡讓它產生固定的名字。
//     但這個函式的正確性不影響評論流程對錯，測它的價值很低，
//     期中不必為此改架構。
//
//  ── 呼叫時機（重要）─────────────────────────────────
//  只在 IsAnonymous = 1 時呼叫。實名評論必須留 NULL，
//  因為 NULL 的意義就是「用會員當下的真實姓名」。
//
//  你說「只有 CreateForVisit 會用上」——這裡要更正一下：
//  CreateForBooking 的 POST 也會用到。預約評論的評論者一定是
//  會員（有 UserId），但會員一樣可以勾匿名。
//  所以是「兩個 POST 都會用，兩個 GET 都不會用」。
// ============================================================

namespace VenueGo.Helpers;

public static class NicknameGenerator
{
    // ── 詞庫 ────────────────────────────────────────────
    // TODO: 把你原本那三組詞貼進來，一組 25 個。
    //       全部保留，一個都不用刪——長度問題在下面用抽選解決。

    // 1. 奇怪形容詞 (原25個 + 新增14個 = 共39個)
    private static readonly string[] Prefixes =
    {
        // 前綴詞，最長 12 字，例：「轉生到異世界也只想躺平的」
        "伸縮自如的", "大智若愚的", "歲月靜好的", "超英趕美的", "自帶BGM的",
        "高清無碼的", "在離職邊緣瘋狂試探的", "剛出爐的", "太想進步的", "CPU燒了的",
        "大力出奇蹟的", "橫衝直撞的", "諧音梗含量超標的", "法力無邊的", "未經授權的",
        "過期五天的", "家裡有礦的", "不知今夕是何年的", "已知用火的", "轉生到異世界也只想躺平的",
        "從地獄歸來的", "原地起飛的", "領域展開的", "颱風天就是要泛舟的", "排水溝過彎失敗的",
        // --- 以下為新增詞組 ---
        "一般路過的", "恨明月不獨照我的", "沒遵守宙斯法則的", "千年難遇的", "精通說話藝術的",
        "從從容容游刃有餘的", "用愛發電的", "在數學課上睡著的", "果汁含量未達10%的", "希望週休七日的",
        "恐懼源於火力不足的", "數值膨脹的", "顏值佔模的", "花語是手慢無的"
    };

    //2. 外觀與狀態形容 (原25個 + 新增3個 = 共28個)
    private static readonly string[] Middles =
    {
        // 外觀詞，最長 4 字，例：「8bit」
        "暗黑", "閃光", "螢光", "迷彩", "黃金", "透明", "金屬", "賽步", "蒸汽",
        "繽紛", "雷射", "復古", "生鏽", "8bit", "軟Q", "光滑", "水嫩", "呆萌",
        "茫然", "冰鎮", "巨大", "迷你", "幻影", "土豪", "液態",
        // --- 以下為新增詞組 ---
        "炫彩", "擬態", "色違"
    };

    //3. 稱謂主體 (原25個 + 新增3個 = 共28個)
    private static readonly string[] Nouns =
    {
        // 生物名詞，最長 6 字，例：「銀喉長尾山雀」
        "海獺", "松鼠", "猩猩", "水豚", "企鵝", "貓咪", "章魚", "樹懶", "狐獴",
        "草泥馬", "柴犬", "水母", "純愛戰神", "迅猛龍", "土撥鼠", "鴨嘴獸", "曼波魚",
        "變色龍", "貓頭鷹", "銀喉長尾山雀", "草履蟲", "獨角獸", "不死鳥", "九尾狐", "霸王龍",
        // --- 以下為新增詞組 ---
        "蝸牛", "螃蟹", "劍齒虎"
    };


    // ── 長度上限 ────────────────────────────────────────
    //
    // 資料庫欄位是 nvarchar(50)，塞得下 22 字，所以這個上限
    // 不是資料庫的要求，是版面的要求：手機上的評論卡片，
    // 名字太長會把星等或日期擠到下一行。
    //
    // 12 + 4 + 6 = 22，超過 20。但這不代表要刪詞——
    // 只代表「轉生到異世界也只想躺平的」這種長前綴，
    // 只會跟短的中間詞與名詞配對。詞全部留著，
    // 產生器負責挑出總長合格的組合。
    public const int MaxLength = 20;


    /// <summary>
    /// 產生一個隨機暱稱。長度不超過 maxLength。
    /// </summary>
    public static string Generate(int maxLength = MaxLength)
    {
        // 隨機抽，抽到太長就重抽。
        // 為什麼是「重抽」而不是「先篩出所有合格組合再抽」：
        // 合格組合有上萬種，先算出來要跑三層迴圈、每次呼叫都算一次，
        // 划不來。重抽幾次就中了。
        for (int i = 0; i < MaxAttempts; i++)
        {
            string candidate = Compose();
            if (candidate.Length <= maxLength)
                return candidate;
        }

        // 保底：連抽 MaxAttempts 次都太長時，用各池最短的詞湊一個。
        // 正常詞庫不會走到這裡，但如果哪天有人把 25 個詞全換成
        // 超長句子，這行能讓程式繼續跑，而不是回一個空字串。
        return ShortestCombination;
    }

    private const int MaxAttempts = 20;


    // ── 內部組裝 ────────────────────────────────────────

    private static string Compose()
        => Pick(Prefixes) + Pick(Middles) + Pick(Nouns);

    // Random.Shared 是 .NET 6 加入的共用實例，執行緒安全。
    // 比每次 new Random() 好——後者在短時間內連續呼叫，
    // 可能拿到相同的種子而產生一樣的結果。
    private static string Pick(string[] pool)
        => pool[Random.Shared.Next(pool.Length)];

    // 各池最短的詞，只算一次。
    // static readonly 欄位在類別第一次被使用時初始化，之後不再重算。
    private static readonly string ShortestCombination =
        Shortest(Prefixes) + Shortest(Middles) + Shortest(Nouns);

    private static string Shortest(string[] pool)
    {
        string best = pool[0];
        foreach (string s in pool)
            if (s.Length < best.Length) best = s;
        return best;
    }
}


// ============================================================
//  在 CreateForVisit 的 [HttpPost] 裡怎麼用
// ============================================================
/*

[HttpPost]
public async Task<IActionResult> CreateForVisit(ReviewCreateInputViewModel vm)
{
    if (!ModelState.IsValid)
        return View(vm);

    // ⚠️ 先處理身分，再決定暱稱——順序不能反。
    //
    // 約束 5-5：UserId IS NULL OR IsAnonymous = 1
    // 免登入同行者沒有身分可顯示，所以「一定」要匿名。
    // 你說前端不顯示匿名選項——對，但表單不送這個欄位時，
    // bool 的預設值是 false，送到後端就變成「實名」，
    // 直接撞上 5-5。所以後端必須自己補這一刀：
    int? userId = _currentUser.MemberId;
    if (userId == null)
        vm.IsAnonymous = true;

    // 只有匿名才產生暱稱，實名留 null（= 用會員真名）
    string? nickname = vm.IsAnonymous
                         ? NicknameGenerator.Generate()
                         : null;

    var review = new ReviewMain
    {
        ReviewPerVisitId  = visit.ReviewPerVisitId,
        ReviewPerBookingId = null,          // XOR：現場評論這欄必為 null
        UserId            = userId,
        StarRating        = vm.StarRating!.Value,
        ReviewContent     = string.IsNullOrWhiteSpace(vm.ReviewContent)
                              ? null        // 純空白要存 null，
                              : vm.ReviewContent,   // 否則撞 CHK_..._Content_NotBlank
        IsAnonymous       = vm.IsAnonymous,
        IsPublic          = vm.IsPublic,
        MentionsVenue     = vm.MentionsVenue,
        MentionsStaff     = vm.MentionsStaff,
        AnonymousNickname = nickname,
        CreatedAt         = DateTime.Now
    };

    // ...存檔、TempData、RedirectToAction
}

*/

// ⚠️ AnonymousNickname 是 db 指令2 PART 1 的欄位，你還沒執行，
//    所以現在資料表裡沒有這一欄，反向工程出來的實體也沒有。
//    上面那段要能跑，指令2 的 PART 1 必須先執行、
//    並在 Models/Entities/ReviewMain.cs 手動補上這個屬性
//    （建議書已說明：不要重跑 scaffold，會覆蓋整個 Entities 資料夾）。
