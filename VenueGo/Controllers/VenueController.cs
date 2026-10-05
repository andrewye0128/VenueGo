using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Net.WebSockets;
using System.Reflection.Metadata.Ecma335;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Models.VenueModels;
using VenueGo.Services;
using VenueGo.ViewModels.VenueViewModels;
using VenueGo.Services.Auth;

namespace VenueGo.Controllers
{
    [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager, RoleNames.Staff)]
    public class VenueController : Controller
    {
        //類別常數
        //每頁顯示3種運動類型分組,固定寫死在這裡,之後要調整頁數大小改這個數字就好
        private const int VenuePageSize = 3;

        //取得照片路徑 >> 取得wwwroot的實際路徑(Controller建構子注入)
        private readonly IWebHostEnvironment _env;

        //取得校時後的系統時間(取代 DateTime.Now,Controller建構子注入)
        private readonly ITimeService _timeService;
        //取得目前登入者資訊(Controller建構子注入)
        private readonly ICurrentUserService _currentUser;

        public VenueController(IWebHostEnvironment env, ITimeService timeService, ICurrentUserService currentUser)
        {
            _env = env;
            _timeService = timeService;
            _currentUser = currentUser;
        }



        /****SportType****/

        //列出所有運動類型
        public IActionResult SportTypeIndex()
        {
            List<CSportTypeWrap> datas = (new CSportTypeFactory()).QueryAll();
            return View(datas);
        }

        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //新增運動類型 >> 頁面產生
        public IActionResult SportTypeCreate()
        {
            return View();
        }


        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //新增運動類型 >> 資料回傳存入DB
        [HttpPost]
        public async Task<IActionResult> SportTypeCreate(CSportTypeWrap Wrap, IFormFile? PhotoFile)
        {
            //檢查運動類型名稱是否重複 >> 若重複,則ModelState.IsValid 會變成 false,並往下落入 if (!ModelState.IsValid)處理
            if (!String.IsNullOrWhiteSpace(Wrap.SportName))
            {
                //對字串進行處理
                Wrap.SportName = Wrap.SportName.Trim();
                //檢查是否現有資料名稱重複
                bool isSportNameDuplicate = new CSportTypeFactory().IsSportNameDuplicate(Wrap.SportName, null);

                if (isSportNameDuplicate)
                {
                    ModelState.AddModelError("SportName", "運動類型名稱已存在,請重新填寫");
                }

            }


            //檢查填寫內容是否合規
            if (!ModelState.IsValid)
                return View(Wrap);

            //取得登入者 UserId(從登入 Cookie 讀取,不從表單傳入,避免被竄改)
            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["VenueErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToAction("SportTypeIndex");
            }

            //存取照片路徑
            Wrap.PhotoPath = await SaveSportTypePhotoAsync(PhotoFile);

            //稽核欄位由後端決定
            Wrap.CreatedAt = _timeService.Now;
            Wrap.CreatedBy = userId.Value;
            //Wrap 直接接表單,可能被夾帶修改紀錄 >> 新增時強制清空
            Wrap.UpdatedAt = null;
            Wrap.UpdatedBy = null;
            Wrap.IsActive = true;

            //存入DB >> 呼叫 Factory 進行 CRUD
            CSportTypeFactory SportTypeFactory = new CSportTypeFactory();
            SportTypeFactory.Create(Wrap);

            return RedirectToAction("SportTypeIndex");
        }


        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //運動類型編輯 >> 頁面產生
        public IActionResult SportTypeEdit(int? id)
        {

            //驗證id非null
            if (id == null)
                return RedirectToAction("SportTypeIndex");


            //用id取出對應資料送到前端 >> 傳入 Factory 操作 Query
            CSportTypeFactory SportTypeFactory = new CSportTypeFactory();
            CSportTypeWrap data = SportTypeFactory.QueryById(id);

            return View(data);
        }



        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //運動類型編輯 >> 參數送回
        [HttpPost]
        public async Task<IActionResult> SportTypeEdit(CSportTypeWrap Wrap, IFormFile? PhotoFile)
        {
            //檢查運動類型名稱是否重複 >> 若重複,則ModelState.IsValid 會變成 false,並往下落入 if (!ModelState.IsValid)處理
            if (!String.IsNullOrWhiteSpace(Wrap.SportName))
            {
                //對字串進行處理
                Wrap.SportName = Wrap.SportName.Trim();
                //檢查是否現有資料名稱重複
                bool isSportNameDuplicate = new CSportTypeFactory().IsSportNameDuplicate(Wrap.SportName, Wrap.SportTypeId);

                if (isSportNameDuplicate)
                {
                    ModelState.AddModelError("SportName", "運動類型名稱已存在,請重新填寫");
                }

            }


            //驗證送回的資料非null
            if (!ModelState.IsValid)
            {
                //表單沒有送回照片路徑 >> 只從DB補回照片,其他欄位保留使用者剛剛填的內容
                Wrap.PhotoPath = new CSportTypeFactory().QueryById(Wrap.SportTypeId).PhotoPath;
                return View(Wrap);
            }

            //取得登入者 UserId(從登入 Cookie 讀取,不從表單傳入,避免被竄改)
            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["VenueErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToAction("SportTypeIndex");
            }

            //有上傳新照片才會有路徑,沒上傳是 null(Factory 會保留原本的照片)
            string? newPhotoPath = await SaveSportTypePhotoAsync(PhotoFile);

            //將前端填寫資料送入 Factory 進行 Edit CRUD
            CSportTypeFactory SportTypeFactory = new CSportTypeFactory();
            SportTypeFactory.Edit(Wrap, newPhotoPath, userId.Value, _timeService.Now);

            return RedirectToAction("SportTypeIndex");
        }


        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //運動類型刪除(軟刪除) >> 刪除前必須確認沒有任何資料還在使用這個運動類型
        //刪除順序:先刪場地 >> 再刪價格規則 >> 最後才能刪運動類型
        //(價格規則的刪除本身也會檢查底下有沒有場地,所以三者的刪除順序是一致的)
        public IActionResult SportTypeDelete(int? id)
        {
            //驗證變數是否為null
            if (id == null)
                return RedirectToAction("SportTypeIndex");

            //取得登入者 UserId(從登入 Cookie 讀取,不從表單傳入,避免被竄改)
            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["VenueErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToAction("SportTypeIndex");
            }

            //確認運動類型存在(查不到或已經刪除過,就不用再刪),順便取得名稱放進訊息
            CSportTypeFactory SportTypeFactory = new CSportTypeFactory();
            CSportTypeWrap sportType = SportTypeFactory.QueryById(id);
            if (sportType.SportTypeId == 0 || !sportType.IsActive)
            {
                TempData["VenueErrorMessage"] = "找不到這個運動類型,可能已經被刪除。";
                return RedirectToAction("SportTypeIndex");
            }

            //檢查一:底下還有存在的場地 >> 擋下(已刪除的場地不算),訊息列出場地名稱,讓使用者知道要先處理哪些場地
            List<string> activeVenueNames = new CVenueFactory().QueryActiveVenueNamesBySportType((int)id);
            if (activeVenueNames.Count > 0)
            {
                TempData["VenueErrorMessage"] = $"「{sportType.SportName}」底下還有 {activeVenueNames.Count} 個場地({string.Join("、", activeVenueNames)}),請先刪除這些場地,才能刪除運動類型。";
                return RedirectToAction("SportTypeIndex");
            }

            //檢查二:還有價格規則 >> 擋下,請使用者先到價格規則管理刪除
            if (new CSportTypePriceRuleFactory().HasPriceRule((int)id))
            {
                TempData["VenueErrorMessage"] = $"「{sportType.SportName}」還有價格規則,請先到「價格規則管理」刪除它的價格規則,才能刪除運動類型。";
                return RedirectToAction("SportTypeIndex");
            }

            //檢查通過 >> 執行軟刪除
            SportTypeFactory.Delete((int)id, userId.Value, _timeService.Now);
            TempData["VenueSuccessMessage"] = $"已成功刪除運動類型「{sportType.SportName}」";

            return RedirectToAction("SportTypeIndex");
        }


        /*Venue*/
        //列出所有場地,依運動類型分組顯示,並依分組換頁
        public IActionResult VenueIndex(int page = 1)
        {
            //查出所有場地資料,依運動類型分組,並依分組換頁
            CVenueFactory venueFactory = new CVenueFactory();
            VenueIndexViewModel vm = venueFactory.QueryGroupedBySportType(page, VenuePageSize);

            return View(vm);
        }


        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //場地新增>> 頁面產生 
        public IActionResult VenueCreate()
        {
            //把運動類型送到前端 >> 做成下拉選單
            //用viewModel存
            var vm = new VenueCreateViewModel();
            vm.SportTypes = new CVenueFactory().GetSportTypes();
            return View(vm);
        }



        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //場地新增 >> 資料回傳存入DB
        [HttpPost]
        public async Task<IActionResult> VenueCreate(VenueCreateViewModel vm)
        {
            //有填寫場地名稱才檢查是否重複
            if (!String.IsNullOrWhiteSpace(vm.VenueName))
            {
                //字串處理 >> 清除空白
                vm.VenueName = vm.VenueName.Trim();
                //呼叫檢查重複方法 >> 檢查傳回的場地名稱是否重複,若重複,則ModelState.IsValid 會變成 false,並往下落入 if (!ModelState.IsValid)處理
                bool isVenueNameDuplicate = new CVenueFactory().IsVenueNameDuplicate(vm.VenueName, null);

                if (isVenueNameDuplicate)
                {
                    ModelState.AddModelError("VenueName", "場地名稱已存在,請重新填寫");
                }

            }

            //判斷填寫欄位是否合規 >> 不合規就重新填寫
            if (!ModelState.IsValid)
            {
                //把下拉選單資料塞回vm回傳前端再填寫一次
                vm.SportTypes = new CVenueFactory().GetSportTypes();
                return View(vm);
            }

            //取得登入者 UserId(從登入 Cookie 讀取,不從表單傳入,避免被竄改)
            //放在照片上傳之前檢查,取不到就不存照片,避免留下沒人用的檔案
            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["VenueErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToAction("VenueIndex");
            }

            //照片上傳
            // null >> 表單有夾帶檔案欄位
            // PhotoFile.Length > 0 >> 確認檔案內容不是空檔案（避免選到大小為 0 byte 的檔案）
            if (vm.PhotoFile != null && vm.PhotoFile.Length > 0)
            {
                // 產生不重複檔名 >> 唯一值 + 原始檔案副檔名
                string fileName = Guid.NewGuid() + Path.GetExtension(vm.PhotoFile.FileName);

                // 實際存放的資料夾路徑 >> 在 wwwroot 路徑下
                string folderPath = Path.Combine(_env.WebRootPath, "images", "venues");

                // 資料夾不存在就先建立，避免 CopyToAsync 找不到路徑報錯
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                //組合路徑 & 檔名
                string filePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await vm.PhotoFile.CopyToAsync(stream);
                }

                // 存進資料庫的是網頁可存取的相對路徑,不是實體硬碟路徑
                vm.PhotoPath = "/images/venues/" + fileName;
            }

            CVenueWrap VenueWrap = new CVenueWrap();
            VenueWrap.VenueName = vm.VenueName;
            VenueWrap.SportTypeId = vm.SportTypeId;
            VenueWrap.Location = vm.Location;
            VenueWrap.IsActive = true;
            VenueWrap.Capacity = vm.Capacity;
            VenueWrap.PhotoPath = vm.PhotoPath;
            VenueWrap.CreatedAt = _timeService.Now;
            VenueWrap.CreatedBy = userId.Value;

            //送進Factory執行新增
            CVenueFactory VenueFactory = new CVenueFactory();
            VenueFactory.Create(VenueWrap);

            //新增完成,回到新場地所在的頁面
            //必須在存檔之後才算:這個運動類型原本可能沒有場地,存檔後才會出現在分組清單裡
            int page = VenueFactory.GetPageBySportTypeId(VenueWrap.SportTypeId, VenuePageSize);
            return RedirectToAction("VenueIndex", new { page = page });
        }



        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //場地編輯 >> 畫面產生
        public IActionResult VenueEdit(int? id)
        {
            //驗證id是否null
            if (id == null)
                return RedirectToAction("VenueIndex");

            //依傳回id執行進查找對應場地資料
            CVenueFactory VenueFactory = new CVenueFactory();
            var data = VenueFactory.QueryById((int)id);
            if (data == null)
                return RedirectToAction("VenueIndex");

            //把data內資料塞進 ViewModel
            VenueEditViewModel vm = new VenueEditViewModel();
            vm.VenueId = (int)id;
            vm.VenueName = data.VenueName;
            vm.SportTypeId = data.SportTypeId;
            vm.Capacity = data.Capacity;
            vm.Location = data.Location;
            vm.PhotoPath = data.PhotoPath;

            //要把運動類型清單一起送到前端
            vm.SportTypes = new CVenueFactory().GetSportTypes();

            //取消、返回列表時回到的頁數 >> 用場地原本的運動類型計算,回到使用者點編輯之前看的那一頁
            vm.ReturnPage = VenueFactory.GetPageBySportTypeId(data.SportTypeId, VenuePageSize);

            return View(vm);
        }


        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //場地編輯 >> 資料傳回
        [HttpPost]
        public async Task<IActionResult> VenueEdit(VenueEditViewModel vm)
        {
                
            //有填寫場地名稱才檢查是否重複
            if (!String.IsNullOrWhiteSpace(vm.VenueName))
            {
                //字串處理 >> 清除空白
                vm.VenueName = vm.VenueName.Trim();
                //呼叫檢查重複方法 >> 檢查傳回的場地名稱是否重複,若重複,則ModelState.IsValid 會變成 false,並往下落入 if (!ModelState.IsValid)處理
                bool isVenueNameDuplicate = new CVenueFactory().IsVenueNameDuplicate(vm.VenueName, vm.VenueId);

                if (isVenueNameDuplicate)
                {
                    ModelState.AddModelError("VenueName", "場地名稱已存在,請重新填寫");
                }
            }


            //驗證欄位填寫是否合規
            if (!ModelState.IsValid)
            {
                //回傳下拉清單選項回去
                vm.SportTypes = new CVenueFactory().GetSportTypes();
                return View(vm);
            }

            //取得登入者 UserId(從登入 Cookie 讀取,不從表單傳入,避免被竄改)
            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["VenueErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToAction("VenueIndex");
            }

            //先處理照片路徑改變 >> 若有上傳照片,就要改變vm的PhotoPath
            if (vm.PhotoFile != null && vm.PhotoFile.Length > 0)
            {
                // 執行照片存邏輯
                string fileName = Guid.NewGuid() + Path.GetExtension(vm.PhotoFile.FileName);


                string folderPath = Path.Combine(_env.WebRootPath, "images", "venues");


                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string filePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await vm.PhotoFile.CopyToAsync(stream);
                }

                // 相對路徑存進資料庫
                vm.PhotoPath = "/images/venues/" + fileName;
            }


            //執行場地資料編輯 >> 送進Factory處理
            CVenueFactory VenueFactory = new CVenueFactory();
            VenueFactory.Edit(vm, userId.Value, _timeService.Now);

            //編輯完成,回到該場地所在的頁面
            //用存檔後的運動類型計算:編輯時改了運動類型,場地會移到新的分組,要跳到新分組所在的頁面
            int page = VenueFactory.GetPageBySportTypeId(vm.SportTypeId, VenuePageSize);
            return RedirectToAction("VenueIndex", new { page = page });
        }


        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //場地刪除
        public IActionResult VenueDelete(int? id)
        {
            //驗證變數是否為null
            if (id == null)
                return RedirectToAction("VenueIndex");

            //取得登入者 UserId(從登入 Cookie 讀取,不從表單傳入,避免被竄改)
            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["VenueErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToAction("VenueIndex");
            }

            //把回傳變數送入 factory 執行軟刪除
            CVenueFactory VenueFactory = new CVenueFactory();
            VenueFactory.Delete((int)id, userId.Value, _timeService.Now);

            return RedirectToAction("VenueIndex");
        }


        /*SporTypePriceRule*/

        //列出所有價格規則 >> 每一筆含尖峰時段摘要與 7 天明細,頁面頂端另外顯示一次場館公休日
        public IActionResult SportTypePriceRuleIndex()
        {
            CSportTypePriceRuleFactory SportTypePriceRuleFactory = new CSportTypePriceRuleFactory();

            SportTypePriceRuleIndexPageViewModel vm = new SportTypePriceRuleIndexPageViewModel();
            vm.Rules = SportTypePriceRuleFactory.QueryAll();
            vm.ClosedDaysText = SportTypePriceRuleFactory.BuildClosedDaysText(new CWeekBusinessHourFactory().QueryAll());

            return View(vm);
        }


        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //價格規則新增 >> 頁面產生
        public IActionResult SportTypePriceRuleCreate()
        {
            CSportTypePriceRuleFactory SportTypePriceRuleFactory = new CSportTypePriceRuleFactory();

            SportTypePriceRuleCreateViewModel vm = new SportTypePriceRuleCreateViewModel();
            //撈出尚未設定價格規則的啟用中運動類型
            vm.SportTypes = SportTypePriceRuleFactory.GetAvailableSportTypes();

            //每週尖峰時段 7 列:新增時沒有任何資料,也不需要舊欄位當預設值,所以 7 天都預設「不分尖峰/離峰」
            //每一列的下拉選項依當天營業時間產生,公休日沒有選項
            List<CWeekBusinessHourWrap> businessHours = new CWeekBusinessHourFactory().QueryAll();
            vm.Days = SportTypePriceRuleFactory.BuildPeakRows(new List<CSportTypePeakHourWrap>(), businessHours, null);

            return View(vm);
        }


        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //價格規則新增 >> 資料回傳存入DB
        [HttpPost]
        public IActionResult SportTypePriceRuleCreate(SportTypePriceRuleCreateViewModel vm)
        {
            CSportTypePriceRuleFactory SportTypePriceRuleFactory = new CSportTypePriceRuleFactory();

            //營業時間查一次,整理、驗證、失敗重新顯示表單都共用這一份
            List<CWeekBusinessHourWrap> businessHours = new CWeekBusinessHourFactory().QueryAll();

            //整理送回來的 7 列(照週一~週日排好、處理缺漏或重複的日子、公休日強制清成 null)
            vm.Days = NormalizePeakDays(vm.Days, businessHours);

            //逐天檢查尖峰起始時間(整點、在當天營業時間內),回傳是否有任何一天設定了尖峰
            bool hasAnyPeak = ValidatePeakDays(vm.Days, businessHours);

            //價格填寫檢查 >> 只要有任何一天有尖峰,尖峰價格就必須大於離峰價格
            if (hasAnyPeak)
            {
                if (vm.PeakPrice <= vm.OffPeakPrice)
                {
                    ModelState.AddModelError(nameof(vm.PeakPrice), "尖峰價格應大於離峰價格");
                }
            }

            //防呆 >> 避免同一個運動類型設定兩筆價格規則,違反唯一索引
            if (SportTypePriceRuleFactory.HasPriceRule(vm.SportTypeId))
            {
                ModelState.AddModelError(nameof(vm.SportTypeId), "此運動類型已經設定過價格規則");
            }

            //先檢查填寫是否通過
            if (!ModelState.IsValid)
            {
                //驗證沒過要重新顯示表單:運動類型下拉選單重新帶回;
                //7 列用使用者送回的值重新組出來(保留使用者選的值,超出範圍的值也會顯示並標示)
                vm.SportTypes = SportTypePriceRuleFactory.GetAvailableSportTypes();
                vm.Days = SportTypePriceRuleFactory.BuildPeakRows(ToPeakHourWraps(vm.Days), businessHours, null);
                return View(vm);
            }

            //取得登入者 UserId(從登入 Cookie 讀取,不從表單傳入,避免被竄改)
            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["VenueErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToAction("SportTypePriceRuleIndex");
            }

            //7 天都不分尖峰/離峰,尖峰價格沒有意義,強制清成0
            //不管前端有沒有正確disable掉輸入框,後端都要保證資料一致(跟WeekBusinessHour的IsOpen=false邏輯一樣)
            if (!hasAnyPeak)
            {
                vm.PeakPrice = 0;
            }

            //回傳的資料存入Wrap
            //舊欄位 PeakStartTime 刻意不寫入(舊欄位不同步);UpdatedAt/UpdatedBy 由 Factory 寫入
            CSportTypePriceRuleWrap Wrap = new CSportTypePriceRuleWrap();
            Wrap.SportTypeId = vm.SportTypeId;
            Wrap.PeakPrice = vm.PeakPrice;
            Wrap.OffPeakPrice = vm.OffPeakPrice;
            Wrap.IsActive = true;

            //存入DB >> 價格規則與 7 天尖峰時間由 Factory 在同一次存檔中一起新增
            //時間用 ITimeService 校時後的時間,由 Controller 取得後傳入 Factory
            SportTypePriceRuleFactory.Create(Wrap, ToPeakHourWraps(vm.Days), userId.Value, _timeService.Now);

            return RedirectToAction("SportTypePriceRuleIndex");
        }



        //價格規則編輯 >> 頁面產生
        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        public IActionResult SportTypePriceRuleEdit(int? id)
        {
            //驗證id非null
            if (id == null)
                return RedirectToAction("SportTypePriceRuleIndex");

            CSportTypePriceRuleFactory SportTypePriceRuleFactory = new CSportTypePriceRuleFactory();

            //用id取出對應資料送到前端
            //QueryById 已經組好 7 列(含每天的選項;改版前的舊價格規則會用舊欄位當預設值)
            SportTypePriceRuleEditViewModel vm = SportTypePriceRuleFactory.QueryById((int)id);
            if (vm == null)
                return RedirectToAction("SportTypePriceRuleIndex");

            return View(vm);
        }

        //價格規則編輯 >> 參數送回
        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        [HttpPost]
        public IActionResult SportTypePriceRuleEdit(SportTypePriceRuleEditViewModel vm)
        {
            CSportTypePriceRuleFactory SportTypePriceRuleFactory = new CSportTypePriceRuleFactory();

            //營業時間查一次,整理、驗證、失敗重新顯示表單都共用這一份
            List<CWeekBusinessHourWrap> businessHours = new CWeekBusinessHourFactory().QueryAll();

            //整理送回來的 7 列(照週一~週日排好、處理缺漏或重複的日子、公休日強制清成 null)
            vm.Days = NormalizePeakDays(vm.Days, businessHours);

            //逐天檢查尖峰起始時間;編輯時遇到已經超出營業時間的舊值,也會在這裡被擋下,必須改成範圍內或不分尖峰/離峰
            bool hasAnyPeak = ValidatePeakDays(vm.Days, businessHours);

            //價格填寫檢查 >> 只要有任何一天有尖峰,尖峰價格就必須大於離峰價格
            if (hasAnyPeak)
            {
                if (vm.PeakPrice <= vm.OffPeakPrice)
                {
                    ModelState.AddModelError(nameof(vm.PeakPrice), "尖峰價格應大於離峰價格");
                }
            }

            //驗證送回的資料
            if (!ModelState.IsValid)
            {
                //驗證沒過要重新顯示表單:7 列用使用者送回的值重新組出來,運動類型名稱也要重新帶回去(表單沒有送回名稱)
                vm.Days = SportTypePriceRuleFactory.BuildPeakRows(ToPeakHourWraps(vm.Days), businessHours, null);
                var data = SportTypePriceRuleFactory.QueryById(vm.SportTypePriceRuleId);
                if (data != null)
                    vm.SportTypeName = data.SportTypeName;
                return View(vm);
            }

            //取得登入者 UserId(從登入 Cookie 讀取,不從表單傳入,避免被竄改)
            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["VenueErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToAction("SportTypePriceRuleIndex");
            }

            //7 天都不分尖峰/離峰,尖峰價格沒有意義,強制清成0
            //不管前端有沒有正確disable掉輸入框,後端都要保證資料一致(跟WeekBusinessHour的IsOpen=false邏輯一樣)
            if (!hasAnyPeak)
            {
                vm.PeakPrice = 0;
            }

            //回傳的資料存入Wrap
            //舊欄位 PeakStartTime 刻意不寫入(舊欄位不同步);UpdatedAt/UpdatedBy 由 Factory 寫入
            CSportTypePriceRuleWrap EditWrap = new CSportTypePriceRuleWrap();
            EditWrap.SportTypePriceRuleId = vm.SportTypePriceRuleId;
            EditWrap.PeakPrice = vm.PeakPrice;
            EditWrap.OffPeakPrice = vm.OffPeakPrice;
            EditWrap.IsActive = vm.IsActive;

            //將前端填寫資料送入 Factory:價格規則與 7 天尖峰時間在同一次存檔中更新(只更新有變動的日子、缺的日子補上)
            //時間用 ITimeService 校時後的時間,由 Controller 取得後傳入 Factory
            SportTypePriceRuleFactory.Edit(EditWrap, ToPeakHourWraps(vm.Days), userId.Value, _timeService.Now);

            return RedirectToAction("SportTypePriceRuleIndex");
        }

        //價格規則刪除 >> 真正的硬刪除,刪除前必須確認該運動類型底下沒有場地正在連動
        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        public IActionResult SportTypePriceRuleDelete(int? id)
        {
            //驗證id非null
            if (id == null)
                return RedirectToAction("SportTypePriceRuleIndex");

            CSportTypePriceRuleFactory SportTypePriceRuleFactory = new CSportTypePriceRuleFactory();

            //先查出這筆價格規則對應的運動類型,才能檢查是否有場地連動,順便確認資料存在
            var data = SportTypePriceRuleFactory.QueryById((int)id);
            if (data == null)
                return RedirectToAction("SportTypePriceRuleIndex");

            //硬刪除前檢查 >> 該運動類型底下若還有場地在用,刪除規則會導致那些場地找不到對應價格,故擋下來
            if (SportTypePriceRuleFactory.HasLinkedVenues(data.SportTypeId))
            {
                TempData["VenueErrorMessage"] = $"「{data.SportTypeName}」目前仍有場地使用中，無法刪除價格規則";
                return RedirectToAction("SportTypePriceRuleIndex");
            }

            //檢查通過,執行硬刪除
            SportTypePriceRuleFactory.Delete((int)id);
            TempData["VenueSuccessMessage"] = $"已成功刪除「{data.SportTypeName}」的價格規則";

            return RedirectToAction("SportTypePriceRuleIndex");
        }


        /*WeekBusinessHour*/

        //場館營業時間管理 >> 頁面產生,一次顯示7天,星期一排最前面、星期日排最後面
        //不管資料表有幾筆,永遠顯示7列;沒有資料的那天標示「本日尚未設定」,預設不營業、時間空白
        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        public IActionResult WeekBusinessHourIndex()
        {
            CWeekBusinessHourFactory WeekBusinessHourFactory = new CWeekBusinessHourFactory();
            List<CWeekBusinessHourWrap> datas = WeekBusinessHourFactory.QueryAll();

            WeekBusinessHourEditViewModel vm = new WeekBusinessHourEditViewModel();
            vm.TimeOptions = WeekBusinessHourFactory.GetWholeHourOptions();

            //GET沒有表單送回的資料,傳null:有資料的那天由BuildBusinessHourRows帶入DB的值
            vm.Days = BuildBusinessHourRows(null, datas);

            return View(vm);
        }

        //場館營業時間管理 >> 參數送回,一次驗證/儲存7天
        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        [HttpPost]
        public IActionResult WeekBusinessHourIndex(WeekBusinessHourEditViewModel vm)
        {
            CWeekBusinessHourFactory WeekBusinessHourFactory = new CWeekBusinessHourFactory();

            //不相信畫面送回的結構,先整理成週一~週日各一列(防竄改),「尚未設定」的標示也依DB重新判斷
            vm.Days = BuildBusinessHourRows(vm.Days, WeekBusinessHourFactory.QueryAll());

            //逐列驗證:IsOpen=true時,OpenTime/CloseTime必填,且OpenTime必須早於CloseTime
            for (int i = 0; i < vm.Days.Count; i++)
            {
                WeekBusinessHourRowViewModel row = vm.Days[i];

                if (row.IsOpen)
                {
                    if (!row.OpenTime.HasValue || !row.CloseTime.HasValue)
                    {
                        ModelState.AddModelError($"Days[{i}].OpenTime", "有營業的當天必須填開始與結束營業時間");
                    }
                    else if (row.OpenTime.Value >= row.CloseTime.Value)
                    {
                        ModelState.AddModelError($"Days[{i}].OpenTime", "開始營業時間必須早於結束營業時間");
                    }
                }
            }

            //驗證沒過要重新顯示表單
            if (!ModelState.IsValid)
            {
                //下拉選單選項要重新帶回去,不然畫面上的選單會是空的
                //DayName、IsNotSet已在BuildBusinessHourRows重新算過,不用另外處理
                vm.TimeOptions = WeekBusinessHourFactory.GetWholeHourOptions();

                return View(vm);
            }

            //取得登入者 UserId(從登入 Cookie 讀取,不從表單傳入,避免被竄改)
            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["VenueErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToAction("WeekBusinessHourIndex");
            }

            //驗證通過,轉成Wrap存回去
            List<CWeekBusinessHourWrap> wraps = new List<CWeekBusinessHourWrap>();

            foreach (WeekBusinessHourRowViewModel row in vm.Days)
            {
                //IsOpen=false時的OpenTime/CloseTime已在BuildBusinessHourRows強制清成null
                CWeekBusinessHourWrap wrap = new CWeekBusinessHourWrap();
                wrap.DayOfWeek = row.DayOfWeek;
                wrap.IsOpen = row.IsOpen;
                wrap.OpenTime = row.OpenTime;
                wrap.CloseTime = row.CloseTime;

                wraps.Add(wrap);
            }

            WeekBusinessHourFactory.EditAll(wraps, userId.Value, _timeService.Now);
            TempData["VenueSuccessMessage"] = "營業時間設定已儲存";

            return RedirectToAction("WeekBusinessHourIndex");
        }

        //組出營業時間表單的7列(GET顯示、POST整理送回資料共用)
        //1. 照週一~週日的順序產生7列,不管資料表有幾筆
        //2. 每一天的值:有表單送回的資料就用送回的(POST);沒有就用DB的資料(GET);兩者都沒有 >> 不營業、時間空白
        //3. 同一天送回好幾列、或DB同一天有好幾筆 >> 只取第一筆
        //4. 不營業的那天,不管送回什麼,開始/打烊時間一律強制清成null(避免「已關閉」還存著時間值)
        //5. IsNotSet(本日尚未設定)只看DB有沒有那天的資料,不看表單
        private List<WeekBusinessHourRowViewModel> BuildBusinessHourRows(List<WeekBusinessHourRowViewModel>? postedDays, List<CWeekBusinessHourWrap> datas)
        {
            DayOfWeek[] displayOrder = new DayOfWeek[]
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
            };

            List<WeekBusinessHourRowViewModel> result = new List<WeekBusinessHourRowViewModel>();

            foreach (DayOfWeek day in displayOrder)
            {
                WeekBusinessHourRowViewModel row = new WeekBusinessHourRowViewModel();
                row.DayOfWeek = day;
                row.DayName = GetDayName(day);

                //找出DB中同一天的第一筆
                CWeekBusinessHourWrap? data = null;
                foreach (CWeekBusinessHourWrap item in datas)
                {
                    if (item.DayOfWeek == day)
                    {
                        data = item;
                        break;
                    }
                }
                row.IsNotSet = data == null;

                //找出送回的資料中同一天的第一列
                WeekBusinessHourRowViewModel? posted = null;
                if (postedDays != null)
                {
                    foreach (WeekBusinessHourRowViewModel item in postedDays)
                    {
                        if (item.DayOfWeek == day)
                        {
                            posted = item;
                            break;
                        }
                    }
                }

                if (postedDays != null)
                {
                    //POST:只用送回的值;某一天沒有送回 >> 當作不營業
                    if (posted != null)
                    {
                        row.IsOpen = posted.IsOpen;
                        row.OpenTime = posted.OpenTime;
                        row.CloseTime = posted.CloseTime;
                    }
                }
                else if (data != null)
                {
                    //GET:帶入DB的值
                    row.IsOpen = data.IsOpen;
                    row.OpenTime = data.OpenTime;
                    row.CloseTime = data.CloseTime;
                }

                if (!row.IsOpen)
                {
                    row.OpenTime = null;
                    row.CloseTime = null;
                }

                result.Add(row);
            }

            return result;
        }

        //依DayOfWeek轉成中文星期名稱,顯示用
        private string GetDayName(DayOfWeek day)
        {
            string name;

            if (day == DayOfWeek.Monday)
            {
                name = "星期一";
            }
            else if (day == DayOfWeek.Tuesday)
            {
                name = "星期二";
            }
            else if (day == DayOfWeek.Wednesday)
            {
                name = "星期三";
            }
            else if (day == DayOfWeek.Thursday)
            {
                name = "星期四";
            }
            else if (day == DayOfWeek.Friday)
            {
                name = "星期五";
            }
            else if (day == DayOfWeek.Saturday)
            {
                name = "星期六";
            }
            else
            {
                name = "星期日";
            }

            return name;
        }


        /*VenueUnavailableSlot*/

        //場地不開放時段管理 >> 頁面產生,顯示某場地某天的所有時段按鈕
        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager, RoleNames.Staff)]
        public IActionResult VenueUnavailableSlotManage(int venueId, DateOnly? date)
        {
            //今天跟現在時間都從同一個校時後的時間拆出來,避免跨午夜時兩者對不上
            DateTime currentTime = _timeService.Now;
            DateOnly today = DateOnly.FromDateTime(currentTime);

            //date防呆 >> 沒帶就設為今天,早於今天就拉回今天,不管網址是不是被手動改過
            DateOnly targetDate;
            if (!date.HasValue || date.Value < today)
            {
                targetDate = today;
            }
            else
            {
                targetDate = date.Value;
            }

            //查場地資訊,查不到代表venueId無效
            CVenueFactory VenueFactory = new CVenueFactory();
            CVenueWrap venue = VenueFactory.QueryById(venueId);
            if (venue == null)
            {
                return RedirectToAction("VenueIndex");
            }

            //查那天的營業時間
            CWeekBusinessHourFactory WeekBusinessHourFactory = new CWeekBusinessHourFactory();
            CWeekBusinessHourWrap businessHour = WeekBusinessHourFactory.GetByDayOfWeek(targetDate.DayOfWeek);

            VenueUnavailableSlotManageViewModel vm = new VenueUnavailableSlotManageViewModel();
            vm.VenueId = venue.VenueId;
            vm.VenueName = venue.VenueName;
            vm.PhotoPath = venue.PhotoPath;
            vm.Date = targetDate;
            vm.Today = today;   //View 用來判斷日期選擇器最小值、是否顯示前一天,不在 View 自己取時間

            //理論上7天資料都已經存在,查不到就當作沒營業處理(異常情況防呆)
            if (businessHour == null || !businessHour.IsOpen)
            {
                vm.IsBusinessDay = false;
                return View(vm);
            }

            vm.IsBusinessDay = true;

            //產生這天的整點時段清單,從OpenTime到CloseTime前一小時
            //用int控制迴圈,理由跟GetWholeHourOptions()一樣:TimeOnly在23:00加1小時會繞回00:00,不能直接拿TimeOnly本身遞增比較
            List<TimeOnly> hourSlots = new List<TimeOnly>();
            for (int hour = businessHour.OpenTime!.Value.Hour; hour < businessHour.CloseTime!.Value.Hour; hour++)
            {
                hourSlots.Add(new TimeOnly(hour, 0));
            }

            //一次查出這天已經被標記不開放的時段,轉成Dictionary方便逐一比對,避免每個時段各查一次DB
            CVenueUnavailableSlotFactory VenueUnavailableSlotFactory = new CVenueUnavailableSlotFactory();
            List<CVenueUnavailableSlotWrap> unavailableSlots = VenueUnavailableSlotFactory.QueryByVenueAndDate(venueId, targetDate);

            Dictionary<TimeOnly, string> reasonByTime = new Dictionary<TimeOnly, string>();
            foreach (var slot in unavailableSlots)
            {
                reasonByTime[slot.UnavailableTime] = slot.Reason;
            }

            //現在的時間,只有targetDate是今天時才會用來判斷IsPast
            TimeOnly now = TimeOnly.FromDateTime(currentTime);

            foreach (TimeOnly time in hourSlots)
            {
                VenueUnavailableSlotHourViewModel row = new VenueUnavailableSlotHourViewModel();
                row.Time = time;

                if (reasonByTime.ContainsKey(time))
                {
                    row.IsUnavailable = true;
                    row.Reason = reasonByTime[time];
                }
                else
                {
                    row.IsUnavailable = false;
                    row.Reason = null;
                }

                if (targetDate == today && time <= now)
                {
                    row.IsPast = true;
                }
                else
                {
                    row.IsPast = false;
                }

                vm.Hours.Add(row);
            }

            return View(vm);
        }


        //場地不開放時段切換 >> 開放變不開放就新增一筆,不開放變開放就刪除那一筆
        //完全不信任前端傳來的「目前是開放還是不開放」,每次都自己用FindByKey重新查一次DB決定
        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager, RoleNames.Staff)]
        [HttpPost]
        public IActionResult VenueUnavailableSlotToggle(int venueId, DateOnly date, TimeOnly time, string? reason)
        {
            //今天跟現在時間都從同一個校時後的時間拆出來,新增時的 CreatedAt 也用同一個值
            DateTime currentTime = _timeService.Now;
            DateOnly today = DateOnly.FromDateTime(currentTime);
            TimeOnly now = TimeOnly.FromDateTime(currentTime);

            //防呆 >> 不能對過去的日期時間做切換,不管前端有沒有正確把按鈕disable掉
            if (date < today || (date == today && time <= now))
            {
                TempData["VenueErrorMessage"] = "已經過去的時段無法設定";
                return RedirectToAction("VenueUnavailableSlotManage", new { venueId, date });
            }

            //取得登入者 UserId(從登入 Cookie 讀取,不從表單傳入,避免被竄改)
            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["VenueErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToAction("VenueUnavailableSlotManage", new { venueId, date });
            }

            CVenueUnavailableSlotFactory VenueUnavailableSlotFactory = new CVenueUnavailableSlotFactory();

            //查詢目前這個時段的真實狀態
            CVenueUnavailableSlotWrap existing = VenueUnavailableSlotFactory.FindByKey(venueId, date, time);

            if (existing != null)
            {
                //目前是不開放,切換回開放 >> 刪除這一筆
                VenueUnavailableSlotFactory.Delete(existing.VenueUnavailableSlotId);
            }
            else
            {
                //目前是開放,切換成不開放 >> 新增一筆,必須要有原因
                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["VenueErrorMessage"] = "請填寫不開放原因";
                    return RedirectToAction("VenueUnavailableSlotManage", new { venueId, date });
                }

                CVenueUnavailableSlotWrap wrap = new CVenueUnavailableSlotWrap();
                wrap.VenueId = venueId;
                wrap.UnavailableDate = date;
                wrap.UnavailableTime = time;
                wrap.Reason = reason;
                wrap.CreatedAt = currentTime;
                wrap.CreatedBy = userId.Value;

                VenueUnavailableSlotFactory.Create(wrap);
            }

            return RedirectToAction("VenueUnavailableSlotManage", new { venueId, date });
        }


        /***** 價格規則「每週尖峰時段」共用的私有方法(SportTypePriceRuleCreate / Edit 的 POST 共用) *****/

        //整理表單送回來的 7 列 >> 不相信畫面送回的結構,一律重新整理成「週一~週日各一列」
        //1. 照週一~週日的順序重新排好
        //2. 某一天沒有送回(表單被竄改或欄位遺失) >> 當作不分尖峰/離峰(null)
        //3. 同一天送回好幾列 >> 只取第一列
        //4. 公休日(依資料庫的營業時間判斷,不看畫面) >> 不管送回什麼,一律強制清成 null
        //   (跟 WeekBusinessHourIndex 的 IsOpen=false 強制清空 OpenTime/CloseTime 是同一個做法)
        private List<SportTypePeakHourRowViewModel> NormalizePeakDays(List<SportTypePeakHourRowViewModel> postedDays, List<CWeekBusinessHourWrap> businessHours)
        {
            DayOfWeek[] displayOrder = new DayOfWeek[]
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
            };

            CSportTypePriceRuleFactory SportTypePriceRuleFactory = new CSportTypePriceRuleFactory();
            List<SportTypePeakHourRowViewModel> result = new List<SportTypePeakHourRowViewModel>();

            foreach (DayOfWeek day in displayOrder)
            {
                SportTypePeakHourRowViewModel row = new SportTypePeakHourRowViewModel();
                row.DayOfWeek = day;
                row.DayName = SportTypePriceRuleFactory.GetShortDayName(day);

                //找出送回的資料中同一天的第一列(postedDays 可能是 null:表單完全沒送 Days)
                row.PeakStartTime = null;
                if (postedDays != null)
                {
                    foreach (SportTypePeakHourRowViewModel posted in postedDays)
                    {
                        if (posted.DayOfWeek == day)
                        {
                            row.PeakStartTime = posted.PeakStartTime;
                            break;
                        }
                    }
                }

                //依資料庫的營業時間判斷是否營業,公休日強制清成 null
                //判斷規則跟 Factory 的 BuildPeakRows 一致:IsOpen=true 而且開始/打烊時間都有值才算營業
                row.IsBusinessDay = false;
                foreach (CWeekBusinessHourWrap businessHour in businessHours)
                {
                    if (businessHour.DayOfWeek == day && businessHour.IsOpen && businessHour.OpenTime.HasValue && businessHour.CloseTime.HasValue)
                    {
                        row.IsBusinessDay = true;
                        row.OpenTime = businessHour.OpenTime;
                        row.CloseTime = businessHour.CloseTime;
                    }
                }

                if (!row.IsBusinessDay)
                {
                    row.PeakStartTime = null;
                }

                result.Add(row);
            }

            return result;
        }


        //逐天檢查尖峰起始時間 >> 規則由 Factory 的 IsPeakStartTimeValid 決定(整點、在當天「開始營業 ~ 打烊前一小時」之間),
        //跟下拉選單、列表提示是同一套規則,不會出現「畫面選得到、存檔卻被擋」的不一致
        //不合法的那一天:錯誤訊息加在那一列(Days[i].PeakStartTime),畫面上顯示在該列下方;
        //另外加一條不屬於任何欄位的總覽錯誤,顯示在表單最上方的 validation summary
        //回傳值:是否有任何一天設定了尖峰(給尖峰價格的檢查與「7 天都沒有尖峰就清成 0」使用)
        //呼叫前 days 必須先經過 NormalizePeakDays 整理(公休日已清成 null、OpenTime/CloseTime 已從資料庫帶入)
        private bool ValidatePeakDays(List<SportTypePeakHourRowViewModel> days, List<CWeekBusinessHourWrap> businessHours)
        {
            CSportTypePriceRuleFactory SportTypePriceRuleFactory = new CSportTypePriceRuleFactory();
            bool hasAnyPeak = false;
            int invalidCount = 0;

            for (int i = 0; i < days.Count; i++)
            {
                SportTypePeakHourRowViewModel day = days[i];

                if (day.PeakStartTime.HasValue)
                {
                    hasAnyPeak = true;
                }

                //找出這一天的營業時間,交給 Factory 判斷是否合法
                CWeekBusinessHourWrap? businessHour = null;
                foreach (CWeekBusinessHourWrap item in businessHours)
                {
                    if (item.DayOfWeek == day.DayOfWeek)
                    {
                        businessHour = item;
                    }
                }

                if (!SportTypePriceRuleFactory.IsPeakStartTimeValid(day.PeakStartTime, businessHour))
                {
                    invalidCount++;

                    //錯誤訊息寫出當天合法的範圍,讓使用者知道要改成幾點
                    string message;
                    if (day.OpenTime.HasValue && day.CloseTime.HasValue)
                    {
                        message = $"{day.DayName} 的尖峰起始時間必須是 {day.OpenTime.Value:HH:mm} ~ {day.CloseTime.Value.AddHours(-1):HH:mm} 之間的整點,或選擇不分尖峰/離峰";
                    }
                    else
                    {
                        message = $"{day.DayName} 的尖峰起始時間不合法,請選擇不分尖峰/離峰";
                    }

                    ModelState.AddModelError($"Days[{i}].PeakStartTime", message);
                }
            }

            if (invalidCount > 0)
            {
                ModelState.AddModelError(string.Empty, $"有 {invalidCount} 天的尖峰起始時間需要修正,請檢查下方標示的日子");
            }

            return hasAnyPeak;
        }


        //把畫面的 7 列轉成 Factory 要的 CSportTypePeakHourWrap 清單(只帶星期與尖峰起始時間,其他欄位由 Factory 填)
        private List<CSportTypePeakHourWrap> ToPeakHourWraps(List<SportTypePeakHourRowViewModel> days)
        {
            List<CSportTypePeakHourWrap> list = new List<CSportTypePeakHourWrap>();

            foreach (SportTypePeakHourRowViewModel day in days)
            {
                CSportTypePeakHourWrap wrap = new CSportTypePeakHourWrap();
                wrap.DayOfWeek = day.DayOfWeek;
                wrap.PeakStartTime = day.PeakStartTime;
                list.Add(wrap);
            }

            return list;
        }


        //存運動類型代表照片 >> 回傳網頁相對路徑;沒有檔案回傳 null
        private async Task<string?> SaveSportTypePhotoAsync(IFormFile? photoFile)
        {
            if (photoFile == null || photoFile.Length == 0)
                return null;

            string fileName = Guid.NewGuid() + Path.GetExtension(photoFile.FileName);
            string folderPath = Path.Combine(_env.WebRootPath, "images", "sporttypes");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            using (var stream = new FileStream(Path.Combine(folderPath, fileName), FileMode.Create))
            {
                await photoFile.CopyToAsync(stream);
            }
            return "/images/sporttypes/" + fileName;
        }
    }
}