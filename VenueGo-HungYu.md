# VenueGo 專案交接文件 — HungYu（場地與時段管理子系統）

> 本文件用途：讓 Claude Code 快速掌握整體專案背景、我負責子系統的設計理念、資料表結構，以及與其他組員模組介接時要注意的事，作為後續在終端機開發的上下文依據。

---

## 一、整體專案背景

- **專案名稱**：VenueGo（運動場地預約系統）
- **性質**：資策會全端 bootcamp 期末團體專題，五人小組，期末發表日 **11/5**
- **技術棧**：ASP.NET Core MVC（.NET，非最新版）、Entity Framework Core（Database First）、SQL Server、Bootstrap 5、jQuery，後續會導入 Vue.js
- **架構模式**：三層式
  - **Entity**（EF Core Scaffold 自動產生，T 前綴或無前綴，**不手動修改**）
  - **Wrap（C 前綴）**：商業邏輯物件，內部**組合持有**（非繼承）Entity 實例，負責 DataAnnotations 驗證（`[Key]`、`[Required]`、`[Display]`）
  - **Factory**：集中 CRUD 邏輯，命名為 `CXxxFactory`，實際上扮演 Repository 角色，Controller 只呼叫 Factory 方法，不直接碰 `DbContext`
- **Controller 慣例**：一個子系統一個 Controller，Action 命名為「表名+動作」避免撞名（例：`VenueIndex`、`VenueCreate`、`SportTypeEdit`）
- **軟刪除**：所有主表都有 `IsActive` 欄位，刪除方法只是把它設為 `false`，查詢時一律加 `WHERE IsActive == true` 過濾
- **不建外鍵約束**：全組共識，資料表之間的關聯完全靠程式邏輯維護，不依賴資料庫層級的 FK；但保留 CHECK constraint 做單表內部驗證
- **EF Core 使用重點**：Database First，用 CLI `dotnet ef dbcontext scaffold`（`Scaffold-DbContext` 在 Git Bash 環境不可用），`--force` 會覆蓋整個 DbContext 檔案，其他組員的 `DbSet` 宣告要記得合併回去
- **Git 工作流程**：Wed push / Thu integration / Fri merge to develop；分支命名 `F{子系統編號}/{姓名}/{功能}`，**巢狀路徑分支會與既有分支名衝突**（例如 `venue` 已存在時不能再開 `venue/xxx`，要改用連字號 `venue-xxx`）

### 團隊分工

| 組員 | 負責子系統 |
|---|---|
| HungYu（我） | 場地與時段管理（Venue & Time Slot） |
| Bo-Yan | 會員/登入（Member & Auth） |
| Zong-Hao（宗豪） | 預約（Reservations） |
| Yu-Lun | 報到（Check-in） |
| Yu-Ling | 營運/數據分析（Operations & Analytics） |

---

## 二、我負責的子系統：場地與時段管理

### 功能範圍

1. 運動類型管理（SportType CRUD）— **已完成**
2. 場地管理（Venue CRUD，含照片上傳）— **已完成**，剛合併進 dev
3. 場地與運動類型前端介面統一美化（進行中）
4. 場館每週固定開放時間管理（WeekBusinessHours）— 待開發
5. 場地不開放時段管理（VenueUnavailableSlots）— 待開發
6. 場地價格規則管理（SportTypePriceRules CRUD + 對外 `GetPrice()` 方法）— 開發中，詳見第六節
7. **核心產出**：計算「某場地在某時間是否可預約」的可用時段運算邏輯（給預約模組 Zong-Hao 呼叫）— 待開發，是本子系統對外最關鍵的輸出

### 現況（已完成部分）

- Venue、SportType 的完整 CRUD 已完成並通過測試，包含：
  - 表單驗證（`ModelState.IsValid`，攤平式 ViewModel + 各欄位獨立 `[Required]`）
  - 照片上傳（`IFormFile` → 存入 `wwwroot/images/venues/`，DB 只存相對路徑字串）
  - 下拉選單（運動類型）的資料綁定與編輯時預選
- 目前正在以下分支上進行：
  - `F2/HUNG-YU/venue-frontend`：場地與運動類型前端美化、風格統一（頁籤導覽 + 卡片式列表）
  - `F2/HUNG-YU/business-hours`：場館開放時間管理
  - `F2/HUNG-YU/unavailable-slots`：場地不開放時段管理
  - `F2/HUNG-YU/venue-SportTypePriceRule`：場地價格規則管理（Scaffold 已完成，正在開發 CRUD 與 `GetPrice()`）
- 所有子分支開發完成後，會依序 `git merge` 進個人總分支 `F2/HUNG-YU/venue`，最後統一 push 給 Git 負責人整合

---

## 三、資料表結構與欄位設計理念

我負責的資料庫共 **5 張表**，全部已定案並同步到 dev：

### 1. SportTypes（運動類型）

| 欄位 | 型別 | 說明 |
|---|---|---|
| SportTypeId | int (PK) | |
| SportName | nvarchar(20) | |
| IsActive | bit | 軟刪除 |
| CreatedAt/By, UpdatedAt/By | | 稽核欄位 |

### 2. Venues（場地）

| 欄位 | 型別 | 說明 |
|---|---|---|
| VenueId | int (PK) | |
| VenueName | nvarchar(40) | |
| SportTypeId | int | 對應運動類型，**一對一**（原設計是多對多，已改為一對一） |
| Location | nvarchar(200) | |
| IsActive | bit | 軟刪除 |
| Capacity | int | |
| PhotoPath | nvarchar(500) | 存**相對路徑字串**（如 `/images/venues/xxx.jpg`），不存實體檔案 |
| CreatedAt/By, UpdatedAt/By | | 稽核欄位 |

### 3. SportTypePriceRules（依運動類型定價）

| 欄位 | 型別 | 說明 |
|---|---|---|
| SportTypePriceRuleId | int (PK, IDENTITY) | |
| SportTypeId | int，唯一索引 `UQ_SportTypePriceRules_SportTypeId` | **定價單位是運動種類、不是個別場地**；一個運動類型只能對應一筆價格規則 |
| PeakStartTime | time(0)，可 null，型別為 `TimeOnly?` | 可為 null：代表這個運動類型不分尖峰離峰，只用 OffPeakPrice 當唯一價格；**限制只能整點**（分秒皆須為 0），因預約時段以一小時為區塊設計，前端 `<input type="time" step="3600">` + 後端驗證雙重把關 |
| PeakPrice / OffPeakPrice | int | 整數金額，無小數，皆加 `[Range(0, int.MaxValue)]` 驗證不可為負數 |
| IsActive | bit，DEFAULT 1 | 軟刪除（已完成，原始 DDL 沒有此欄位，已用 `ALTER TABLE` 補上並重新 Scaffold） |
| UpdatedAt / UpdatedBy | datetime2(0) / int，皆可 null | 稽核欄位（此表無 CreatedAt/By，只有 UpdatedAt/By） |

> **尖峰/離峰規則（一天只切一刀，不分平日／周末）**：`PeakStartTime` 之前算離峰、`PeakStartTime` 開始一路到營業結束都算尖峰。**目前不支援依平日／假日設定不同規則**——這是已知的潛在擴充需求，非目前交付範圍。

### 4. WeekBusinessHours（場館每週固定營業時間）

| 欄位 | 型別 | 說明 |
|---|---|---|
| BusinessHoursId | int (PK, IDENTITY) | |
| DayOfWeek | int，對應 C# 內建 `System.DayOfWeek` enum（0=Sunday ~ 6=Saturday） | 採用 .NET 內建列舉編碼，可直接用 `DateTime.DayOfWeek` 比對，不需額外轉換邏輯 |
| IsOpen | bit | |
| OpenTime / CloseTime | time(0) | |

> 這張表管的是**整個場館共用**的規則，不分場地。例：週一到週五 9:00–21:00 開放。
> `DayOfWeek` 欄位存的數字直接對應 C# 的 `System.DayOfWeek` enum（星期日 = 0，星期六 = 6），查詢時可直接用 `(int)someDateTime.DayOfWeek` 取得對應值比對，不需自訂轉換規則。


### 5. VenueUnavailableSlots（場地不開放例外時段）

| 欄位 | 型別 | 說明 |
|---|---|---|
| VenueId | int | |
| UnavailableDate | date | |
| UnavailableTime | time(0)（原名 `UnavailableStartTime`，已改名） | **只存起始時間**，結束時間 = 起始 + 1 小時，由程式計算，不存資料庫 |
| Reason | nvarchar(500) | |
| 複合唯一索引 | `UQ_VenueUnavailableSlots_VenueDateTime`（VenueId + UnavailableDate + UnavailableTime） | |

> 這張表**只存「不開放」的例外格**，不存整天狀態；時段切割為一小時一格。

### 資料表設計通則

- 時間精度全表統一為 `time(0)`（曾發生彙整腳本誤設為 `(7)` 的問題，已修正）
- 字串欄位長度依實際用途設定（曾發生 `nvarchar` 誤設為長度 1 的問題，已修正）
- 不建外鍵約束，關聯靠程式邏輯維護
- `int` 型別不可能是 `null`，對其加 `[Required]` 或做 `== null` 判斷都是無效程式碼；只有 `FirstOrDefault` 查無資料時，物件本身才會是 `null`，這才需要判斷

---

## 四、與外部模組溝通的注意事項

1. **不建外鍵約束，所有跨模組關聯靠程式邏輯維護**：其他組員的表（例如預約表）如果要參照 `VenueId`、`SportTypeId`，資料庫層級不會強制檢查，呼叫方要自己確保 ID 的有效性（例如查詢前先確認 `IsActive == true`）

2. **對預約模組（Zong-Hao）的介接點一：`GetPrice()` 價格查詢**（開發中，詳見第六節）：
   - 方法簽名：`int GetPrice(int venueId, TimeSpan reservationTime)`
   - Zong-Hao 只需傳「場地 ID」+「預約時間（用 `DateTime.TimeOfDay` 取得）」，內部自行處理場地→運動類型→價格規則的轉換與尖峰離峰判斷，直接回傳整數金額
   - **此方法不檢查時段是否合法可預約**，呼叫方需自行確保傳入的時間是合法的可預約時段（銜接第 3 點）

3. **對預約模組（Zong-Hao）最關鍵的介接點二**：可用時段運算邏輯（尚未開發，即 `IsAvailable()`）。這個邏輯需要綜合三張表才能算出「某場地在某天某時段是否可約」：
   - `WeekBusinessHours`（場館當天是否開放、開放時段）
   - `VenueUnavailableSlots`（該場地當天是否有被標記不開放的例外格）
   - 這是「送出預約、寫入資料前」的最後把關，不是 `GetPrice()` 的職責；目前尚未設計好對外的方法簽名，排在 `unavailable-slots` 分支開發

4. **軟刪除的影響**：任何模組查詢 `Venues`、`SportTypes`、`SportTypePriceRules` 時，都必須加上 `IsActive == true` 的過濾條件，否則會撈到已「刪除」的資料

5. **照片路徑格式**：`PhotoPath` 存的是**相對於 `wwwroot` 的網址路徑**（如 `/images/venues/xxx.jpg`），不是實體磁碟路徑；其他模組如果要顯示場地照片，直接把這個字串放進 `<img src="">` 即可，不需要額外組路徑

6. **EF Core 追蹤陷阱（團隊共同踩過的坑）**：同一個方法內，查詢資料與 `SaveChanges()` 必須使用**同一個 `DbContext` 實例**，否則 `SaveChanges()` 會「白跑」（不會真正寫入資料庫），這個坑在 Edit 功能開發時發生過兩次

7. **Git 分支合併節奏**：週三 push、週四整合、週五 merge to dev；如果其他模組的分支跟我的分支有共用檔案（例如共用的 `_Layout.cshtml`、共用的 ViewModel 資料夾），建議整合日之前先互相知會，減少衝突

---

## 六、場地價格規則功能（SportTypePriceRules）開發規格

> 分支：`F2/HUNG-YU/venue-SportTypePriceRule`

### 前置事項（已完成）

`IsActive` 欄位已用 `ALTER TABLE` 補上並重新 Scaffold 同步。**本專案 Scaffold 指令務必帶滿以下參數**（血淚教訓：漏帶會導致命名空間衝突、DbContext 被生到錯誤資料夾）：

```bash
dotnet ef dbcontext scaffold "連線字串;Command Timeout=120" Microsoft.EntityFrameworkCore.SqlServer -o Models/Entities --context-dir Data --context dbVenueContext --namespace VenueGo.Models.Entities --context-namespace VenueGo.Data --no-onconfiguring --force
```

（對應 Package Manager Console：`Scaffold-DbContext ... -OutputDir Models/Entities -ContextDir Data -Context dbVenueContext -Namespace VenueGo.Models.Entities -ContextNamespace VenueGo.Data -NoOnConfiguring -Force`；團隊習慣用 Package Manager Console 執行，Git Bash 不支援 `Scaffold-DbContext`。`dbVenueContext.Config.cs` 是手動維護、不會被覆蓋的 partial class。）

### 核心設計原則

- 定價單位是「運動類型」，一個運動類型只能對應一筆價格規則（唯一索引限制）
- 尖峰/離峰依「一天中的時間點」切一刀，不分平日／周末
- `PeakStartTime` 為 `null` 時代表不分尖峰離峰，統一用 `OffPeakPrice`
- `PeakStartTime` 限制只能整點（分秒皆為 0），因預約時段以一小時為單位設計，避免同一區塊被切成一半尖峰一半離峰
- 價格為整數，且不可為負數

### 功能入口與導覽

- 入口放在 Venue 模組既有 nav 頁籤，新增一個頁籤項目，導向 `SportTypePriceRuleIndex`
- Index 列出所有運動類型的價格設定狀態，導向 `SportTypePriceRuleCreate`（尚未設定）或 `SportTypePriceRuleEdit`（已設定）
- **刪除功能已開發完成**：`IsActive` 開關（在 Edit 頁面）作為軟刪除／停用用途；另外提供真正的硬刪除 `SportTypePriceRuleDelete` Action，刪除前必須用 `HasLinkedVenues(sportTypeId)` 檢查該運動類型底下是否還有場地在用，有連動就擋下（`TempData["ErrorMessage"]` 顯示原因），沒有才真正 `Remove` 該筆資料

### Action 一覽（併入既有 VenueController）

`SportTypePriceRuleIndex`（GET）／`SportTypePriceRuleCreate`（GET/POST，POST 需防呆檢查該 `SportTypeId` 是否已存在規則）／`SportTypePriceRuleEdit`（GET/POST）／`SportTypePriceRuleDelete`（GET，硬刪除，刪除前檢查場地連動）

### 對外方法一：`GetPrice()` — 計算預約總價（已開發完成）

```csharp
/// <summary>
/// 依場地與預約的多個時段區塊，計算總價格。每個時段代表一小時，
/// 呼叫方傳入該場地所有預約區塊的起始時間清單（時段不重複，前端已保證）。
/// 場地不存在（含已軟刪除）或查無價格規則時拋出 KeyNotFoundException，呼叫方須自行 try-catch。
/// </summary>
public int GetPrice(int venueId, List<TimeSpan> reservationTimes)
```

邏輯：查一次價格規則（共用私有方法 `GetPriceRuleForVenue()`）→ `foreach` 每個時段依 `PeakStartTime` 判斷尖峰/離峰（`>=` 含起始點算尖峰）→ 加總回傳；空清單或 `null` 回傳 0。

### 對外方法二：`GetPriceRule()` — 查詢價格（顯示用途，例如場地卡片）（已開發完成）

```csharp
/// <summary>
/// 依場地查出尖峰/離峰價格，供顯示用途，不做時段判斷或加總。
/// 若該運動類型不分尖峰離峰，PeakPrice 回傳 null（即使資料庫有存數值）。
/// 場地不存在（含已軟刪除）或查無價格規則時拋出 KeyNotFoundException，呼叫方須自行 try-catch。
/// </summary>
public VenuePriceInfo GetPriceRule(int venueId)

public class VenuePriceInfo
{
    public int? PeakPrice { get; set; }
    public int OffPeakPrice { get; set; }
}
```

### 共用私有方法（已開發完成）

```csharp
/// <summary>
/// 查場地（過濾 IsActive）→ 查價格規則（過濾 IsActive）→ 回傳；查不到任一者皆拋出 KeyNotFoundException。
/// GetPrice() 與 GetPriceRule() 皆呼叫此方法，避免查詢邏輯重複。
/// </summary>
private CSportTypePriceRuleWrap GetPriceRuleForVenue(int venueId)
```

**測試狀況**：已對照真實資料庫實測過以下情境，皆符合預期——有尖峰規則的場地（含尖峰起始時間邊界值 `>=` 判斷）、空清單／`null`清單、場地已軟刪除、場地不存在、場地存在但該運動類型尚未設定價格規則。測試方式是另建一個丟棄式 console 專案呼叫 Factory 方法打真實 DB，測完即刪除，未寫入正式測試專案。**尚未做的是跨模組整合測試**——預約模組（Zong-Hao）還沒有實際呼叫過這兩個方法。

### 明確的職責邊界

- 兩個方法皆不檢查時段是否合法可預約（那是 `IsAvailable()` 的職責，尚未開發，排在 `unavailable-slots` 分支）
- `GetPrice()` 不檢查時段重複；`GetPriceRule()` 回傳的 `PeakPrice` 是 `int?`，跟 Wrap 本身的 `int PeakPrice` 型別語意不同，不要混用

### 開發範圍界定

**包含**：`CSportTypePriceRuleWrap`、`CSportTypePriceRuleFactory`、四個 Action（含 `SportTypePriceRuleDelete` 硬刪除）、對應 View（含 nav 頁籤）、`GetPrice()`／`GetPriceRule()`／`GetPriceRuleForVenue()`、`VenuePriceInfo`、`PeakStartTime` 整點限制（前後端）

**已開發完成**：`CSportTypePriceRuleWrap`、`CSportTypePriceRuleFactory`（含 `HasLinkedVenues()`、`Delete()`、`GetPrice()`、`GetPriceRule()`、`GetPriceRuleForVenue()`）、`VenuePriceInfo`、Index/Create/Edit/Delete 四個 Action 與對應 View、nav 頁籤、`PeakStartTime` 整點限制（前後端）。`GetPrice()`／`GetPriceRule()` 已對照真實 DB 單元驗證過，跨模組整合測試待預約模組實際呼叫後才算完整驗證。

**不包含**：`IsAvailable()`、依平日／假日區分尖峰離峰規則（已知潛在需求，非本次交付）

## 七、本週（接下來 4 天）工作項目

1. 統一並完善場地管理功能的前端介面，以及前端表單輸入管控（分支：`F2/HUNG-YU/venue-frontend`）
2. 修飾運動類型管理的前端介面（同上分支）
3. 統一兩者的前端風格（頁籤導覽 + 卡片式列表，同上分支）
4. 開發場館開放時間管理（分支：`F2/HUNG-YU/business-hours`）
5. 開發場地開放時段管理（分支：`F2/HUNG-YU/unavailable-slots`）
6. 開發場地價格規則管理，含對外 `GetPrice()` 方法（分支：`F2/HUNG-YU/venue-SportTypePriceRule`，進行中）
