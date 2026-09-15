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
6. **核心產出**：計算「某場地在某時間是否可預約」的可用時段運算邏輯（給預約模組 Zong-Hao 呼叫）— 待開發，是本子系統對外最關鍵的輸出

### 現況（已完成部分）

- Venue、SportType 的完整 CRUD 已完成並通過測試，包含：
  - 表單驗證（`ModelState.IsValid`，攤平式 ViewModel + 各欄位獨立 `[Required]`）
  - 照片上傳（`IFormFile` → 存入 `wwwroot/images/venues/`，DB 只存相對路徑字串）
  - 下拉選單（運動類型）的資料綁定與編輯時預選
- 目前正在三條新分支上進行：
  - `F2/HUNG-YU/venue-frontend`：場地與運動類型前端美化、風格統一（頁籤導覽 + 卡片式列表）
  - `F2/HUNG-YU/business-hours`：場館開放時間管理
  - `F2/HUNG-YU/unavailable-slots`：場地不開放時段管理

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
| SportTypeId | int，唯一索引 `UQ_SportTypePriceRules_SportTypeId` | **定價單位是運動種類、不是個別場地** |
| PeakStartTime | time(0)，可 null | |
| PeakPrice / OffPeakPrice | | |

### 4. WeekBusinessHours（場館每週固定營業時間）

| 欄位 | 型別 | 說明 |
|---|---|---|
| BusinessHoursId | int (PK, IDENTITY) | |
| DayOfWeek | | |
| IsOpen | bit | |
| OpenTime / CloseTime | time(0) | |

> 這張表管的是**整個場館共用**的規則，不分場地。例：週一到週五 9:00–21:00 開放。

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

2. **對預約模組（Zong-Hao）最關鍵的介接點**：可用時段運算邏輯（尚未開發）。這個邏輯需要綜合三張表才能算出「某場地在某天某時段是否可約」：
   - `WeekBusinessHours`（場館當天是否開放、開放時段）
   - `VenueUnavailableSlots`（該場地當天是否有被標記不開放的例外格）
   - 目前尚未設計好對外的方法簽名（method signature），這是這週要優先確定的介面

3. **軟刪除的影響**：任何模組查詢 `Venues`、`SportTypes` 時，都必須加上 `IsActive == true` 的過濾條件，否則會撈到已「刪除」的資料

4. **照片路徑格式**：`PhotoPath` 存的是**相對於 `wwwroot` 的網址路徑**（如 `/images/venues/xxx.jpg`），不是實體磁碟路徑；其他模組如果要顯示場地照片，直接把這個字串放進 `<img src="">` 即可，不需要額外組路徑

5. **EF Core 追蹤陷阱（團隊共同踩過的坑）**：同一個方法內，查詢資料與 `SaveChanges()` 必須使用**同一個 `DbContext` 實例**，否則 `SaveChanges()` 會「白跑」（不會真正寫入資料庫），這個坑在 Edit 功能開發時發生過兩次

6. **Git 分支合併節奏**：週三 push、週四整合、週五 merge to dev；如果其他模組的分支跟我的分支有共用檔案（例如共用的 `_Layout.cshtml`、共用的 ViewModel 資料夾），建議整合日之前先互相知會，減少衝突

---

## 五、本週（接下來 4 天）工作項目

1. 統一並完善場地管理功能的前端介面，以及前端表單輸入管控（分支：`F2/HUNG-YU/venue-frontend`）
2. 修飾運動類型管理的前端介面（同上分支）
3. 統一兩者的前端風格（頁籤導覽 + 卡片式列表，同上分支）
4. 開發場館開放時間管理（分支：`F2/HUNG-YU/business-hours`）
5. 開發場地開放時段管理（分支：`F2/HUNG-YU/unavailable-slots`）
