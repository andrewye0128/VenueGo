using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Net.WebSockets;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Models.VenueModels;
using VenueGo.ViewModels.VenueViewModels;

namespace VenueGo.Controllers
{
    [EmployeeAuthorize(RoleNames.Admin,RoleNames.Manager,RoleNames.Staff)]
    public class VenueController : Controller
    {
        //取得照片路徑 >> 取得wwwroot的實際路徑(Controller建構子注入)
        private readonly IWebHostEnvironment _env;
        public VenueController(IWebHostEnvironment env)
        {
            _env = env;
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
        public IActionResult SportTypeCreate(CSportTypeWrap Wrap)
        {
            //先檢查填寫是否通過
            if (!ModelState.IsValid)
            {
                return View(Wrap);
            }

            //設定暫時預設欄位值 >> 要回來改
            Wrap.CreatedBy = 1; //尚未整合會員ID追蹤,先給1
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
        public IActionResult SportTypeEdit(CSportTypeWrap Wrap)
        {
            //驗證送回的資料非null
            if (!ModelState.IsValid)
                return View(Wrap);

            //將前端填寫資料送入 Factory 進行 Edit CRUD
            CSportTypeFactory SportTypeFactory = new CSportTypeFactory();
            SportTypeFactory.Edit(Wrap);

            return RedirectToAction("SportTypeIndex");
        }


        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //運動類型刪除
        public IActionResult SportTypeDelete(int? id)
        {
            //驗證變數是否為null
            if (id == null)
                return RedirectToAction("SportTypeIndex");


            //把回傳變數送入 factory 執行軟刪除
            CSportTypeFactory SportTypeFactory = new CSportTypeFactory();
            SportTypeFactory.Delete((int)id);

            return RedirectToAction("SportTypeIndex");
        }


        /*Venue*/
        //列出所有場地,依運動類型分組顯示,並依分組換頁
        public IActionResult VenueIndex(int page = 1)
        {
            //每頁顯示3種運動類型分組,固定寫死在這裡,之後要調整頁數大小改這個數字就好
            int pageSize = 3;

            CVenueFactory venueFactory = new CVenueFactory();
            VenueIndexViewModel vm = venueFactory.QueryGroupedBySportType(page, pageSize);

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
            //判斷填寫欄位是否合規 >> 不合規就重新填寫
            if (!ModelState.IsValid)
            {
                //把下拉選單資料塞回vm回傳前端再填寫一次
                vm.SportTypes = new CVenueFactory().GetSportTypes();
                return View(vm);
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
            VenueWrap.CreatedBy = 1;//尚未整合會員權限,先設為1

            //送進Factory執行新增
            CVenueFactory VenueFactory = new CVenueFactory();
            VenueFactory.Create(VenueWrap);

            return RedirectToAction("VenueIndex");
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


            return View(vm);
        }


        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //場地編輯 >> 資料傳回
        [HttpPost]
        public async Task<IActionResult> VenueEdit(VenueEditViewModel vm)
        {
            //驗證欄位填寫是否合規
            if (!ModelState.IsValid)
            {
                //回傳下拉清單選項回去
                vm.SportTypes = new CVenueFactory().GetSportTypes();
                return View(vm);
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
            VenueFactory.Edit(vm);



            //編輯完成,回到場地清單
            return RedirectToAction("VenueIndex");
        }


        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //場地刪除
        public IActionResult VenueDelete(int? id)
        {
            //驗證變數是否為null
            if (id == null)
                return RedirectToAction("VenueIndex");


            //把回傳變數送入 factory 執行軟刪除
            CVenueFactory VenueFactory = new CVenueFactory();
            VenueFactory.Delete((int)id);

            return RedirectToAction("VenueIndex");
        }


        /*SporTypePriceRule*/

        //列出所有價格規則
        public IActionResult SportTypePriceRuleIndex()
        {
            CSportTypePriceRuleFactory SportTypePriceRuleFactory = new CSportTypePriceRuleFactory();
            List<SportTypePriceRuleIndexViewModel> vm = SportTypePriceRuleFactory.QueryAll();

            return View(vm);
        }


        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //價格規則新增 >> 頁面產生
        public IActionResult SportTypePriceRuleCreate()
        {
            //傳運動類型名稱到前端
            SportTypePriceRuleCreateViewModel vm = new SportTypePriceRuleCreateViewModel();
            //撈出尚未設定價格規則的啟用中運動類型
            vm.SportTypes = new CSportTypePriceRuleFactory().GetAvailableSportTypes();
            //尖峰起始時間下拉選單選項(只列出合法的整點時間,UI 上就不會選得出不合法的值)
            vm.PeakStartTimeOptions = new CSportTypePriceRuleFactory().GetPeakStartTimeOptions();
            return View(vm);
        }


        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        //價格規則新增 >> 資料回傳存入DB
        [HttpPost]
        public IActionResult SportTypePriceRuleCreate(SportTypePriceRuleCreateViewModel vm)
        {
            CSportTypePriceRuleFactory SportTypePriceRuleFactory = new CSportTypePriceRuleFactory();

            //整點檢查 >> PeakStartTime 有值時,分鐘/秒數必須是0
            //前端已經改成下拉選單、選項本身就只有整點,這裡是防止有人跳過前端直接送 POST(例如用 Postman)
            if (!IsWholeHour(vm.PeakStartTime))
            {
                ModelState.AddModelError(nameof(vm.PeakStartTime), "尖峰起始時間只能設定整點");
            }

            //營業時間範圍檢查 >> PeakStartTime 有值時,必須落在營業時間內,且不能晚於打烊前一小時
            //⚠️【暫時寫死,待接續開發】範圍目前來自 CSportTypePriceRuleFactory 裡寫死的營業時間常數,
            //還沒有真正查詢 WeekBusinessHour,等那個功能做完要回來把這段檢查改成查表
            if (!IsWithinBusinessHours(vm.PeakStartTime))
            {
                ModelState.AddModelError(nameof(vm.PeakStartTime),
                    $"尖峰起始時間需介於 {CSportTypePriceRuleFactory.BusinessOpenTime:HH:mm} ~ {CSportTypePriceRuleFactory.LatestPeakStartTime:HH:mm} 之間");
            }

            //防呆 >> 避免同一個運動類型設定兩筆價格規則,違反唯一索引
            if (SportTypePriceRuleFactory.HasPriceRule(vm.SportTypeId))
            {
                ModelState.AddModelError(nameof(vm.SportTypeId), "此運動類型已經設定過價格規則");
            }

            //先檢查填寫是否通過
            if (!ModelState.IsValid)
            {
                vm.SportTypes = SportTypePriceRuleFactory.GetAvailableSportTypes();
                //驗證沒過要重新顯示表單,下拉選單選項也要重新帶回去,不然畫面上的選單會是空的
                vm.PeakStartTimeOptions = SportTypePriceRuleFactory.GetPeakStartTimeOptions();
                return View(vm);
            }

            //尖峰起始時間沒填,代表不分尖峰/離峰,尖峰價格沒有意義,強制清成0
            //不管前端有沒有正確disable掉輸入框,後端都要保證資料一致(跟WeekBusinessHour的IsOpen=false邏輯一樣)
            if (!vm.PeakStartTime.HasValue)
            {
                vm.PeakPrice = 0;
            }

            //回傳的資料存入Wrap
            CSportTypePriceRuleWrap Wrap = new CSportTypePriceRuleWrap();
            Wrap.SportTypeId = vm.SportTypeId;
            Wrap.PeakStartTime = vm.PeakStartTime;
            Wrap.PeakPrice = vm.PeakPrice;
            Wrap.OffPeakPrice = vm.OffPeakPrice;
            Wrap.IsActive = true;

            //存入DB >> 呼叫 Factory 進行 CRUD
            SportTypePriceRuleFactory.Create(Wrap);

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
            SportTypePriceRuleEditViewModel vm = SportTypePriceRuleFactory.QueryById((int)id);
            if (vm == null)
                return RedirectToAction("SportTypePriceRuleIndex");

            //尖峰起始時間下拉選單選項
            vm.PeakStartTimeOptions = SportTypePriceRuleFactory.GetPeakStartTimeOptions();

            return View(vm);
        }

        //價格規則編輯 >> 參數送回
        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        [HttpPost]
        public IActionResult SportTypePriceRuleEdit(SportTypePriceRuleEditViewModel vm)
        {
            CSportTypePriceRuleFactory SportTypePriceRuleFactory = new CSportTypePriceRuleFactory();

            //整點檢查 >> PeakStartTime 有值時,分鐘/秒數必須是0
            if (!IsWholeHour(vm.PeakStartTime))
            {
                ModelState.AddModelError(nameof(vm.PeakStartTime), "尖峰起始時間只能設定整點");
            }

            //營業時間範圍檢查 >> PeakStartTime 有值時,必須落在營業時間內,且不能晚於打烊前一小時
            //⚠️【暫時寫死,待接續開發】範圍目前來自 CSportTypePriceRuleFactory 裡寫死的營業時間常數,
            //還沒有真正查詢 WeekBusinessHour,等那個功能做完要回來把這段檢查改成查表
            if (!IsWithinBusinessHours(vm.PeakStartTime))
            {
                ModelState.AddModelError(nameof(vm.PeakStartTime),
                    $"尖峰起始時間需介於 {CSportTypePriceRuleFactory.BusinessOpenTime:HH:mm} ~ {CSportTypePriceRuleFactory.LatestPeakStartTime:HH:mm} 之間");
            }

            //驗證送回的資料非null
            if (!ModelState.IsValid)
            {
                //驗證沒過要重新顯示表單,下拉選單選項跟運動類型名稱都要重新帶回去
                vm.PeakStartTimeOptions = SportTypePriceRuleFactory.GetPeakStartTimeOptions();
                var data = SportTypePriceRuleFactory.QueryById(vm.SportTypePriceRuleId);
                if (data != null)
                    vm.SportTypeName = data.SportTypeName;
                return View(vm);
            }

            //尖峰起始時間沒填,代表不分尖峰/離峰,尖峰價格沒有意義,強制清成0
            //不管前端有沒有正確disable掉輸入框,後端都要保證資料一致(跟WeekBusinessHour的IsOpen=false邏輯一樣)
            if (!vm.PeakStartTime.HasValue)
            {
                vm.PeakPrice = 0;
            }

            //回傳的資料存入Wrap
            CSportTypePriceRuleWrap EditWrap = new CSportTypePriceRuleWrap();
            EditWrap.SportTypePriceRuleId = vm.SportTypePriceRuleId;
            EditWrap.PeakStartTime = vm.PeakStartTime;
            EditWrap.PeakPrice = vm.PeakPrice;
            EditWrap.OffPeakPrice = vm.OffPeakPrice;
            EditWrap.IsActive = vm.IsActive;

            //將前端填寫資料送入 Factory 進行 Edit CRUD
            SportTypePriceRuleFactory.Edit(EditWrap);

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
        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        public IActionResult WeekBusinessHourIndex()
        {
            CWeekBusinessHourFactory WeekBusinessHourFactory = new CWeekBusinessHourFactory();
            List<CWeekBusinessHourWrap> datas = WeekBusinessHourFactory.QueryAll();

            //把星期一排最前面、星期日排最後面
            //QueryAll()回傳的是DayOfWeek 0~6的原始順序,星期日(0)會排最前面,所以這裡另外排序一次
            var orderedDatas = datas.OrderBy(data =>
            {
                int sortKey;
                if (data.DayOfWeek == DayOfWeek.Sunday)
                {
                    sortKey = 7;
                }
                else
                {
                    sortKey = (int)data.DayOfWeek;
                }
                return sortKey;
            });

            WeekBusinessHourEditViewModel vm = new WeekBusinessHourEditViewModel();
            vm.TimeOptions = WeekBusinessHourFactory.GetWholeHourOptions();

            foreach (var data in orderedDatas)
            {
                WeekBusinessHourRowViewModel row = new WeekBusinessHourRowViewModel();
                row.BusinessHoursId = data.BusinessHoursId;
                row.DayOfWeek = data.DayOfWeek;
                row.DayName = GetDayName(data.DayOfWeek);
                row.IsOpen = data.IsOpen;
                row.OpenTime = data.OpenTime;
                row.CloseTime = data.CloseTime;
                vm.Days.Add(row);
            }

            return View(vm);
        }

        //場館營業時間管理 >> 參數送回,一次驗證/儲存7天
        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]
        [HttpPost]
        public IActionResult WeekBusinessHourIndex(WeekBusinessHourEditViewModel vm)
        {
            CWeekBusinessHourFactory WeekBusinessHourFactory = new CWeekBusinessHourFactory();

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
                vm.TimeOptions = WeekBusinessHourFactory.GetWholeHourOptions();

                //DayName是[ValidateNever],表單送回來時不會帶值,要用DayOfWeek(隱藏欄位)重新算一次,不然畫面上星期名稱會不見
                for (int i = 0; i < vm.Days.Count; i++)
                {
                    vm.Days[i].DayName = GetDayName(vm.Days[i].DayOfWeek);
                }

                return View(vm);
            }

            //驗證通過,轉成Wrap存回去
            List<CWeekBusinessHourWrap> wraps = new List<CWeekBusinessHourWrap>();

            foreach (WeekBusinessHourRowViewModel row in vm.Days)
            {
                CWeekBusinessHourWrap wrap = new CWeekBusinessHourWrap();
                wrap.BusinessHoursId = row.BusinessHoursId;
                wrap.DayOfWeek = row.DayOfWeek;
                wrap.IsOpen = row.IsOpen;

                //IsOpen=false時,不管前端有沒有正確disable掉選單、送回來的值是什麼,後端一律強制清成null,
                //避免「已關閉」的當天還存著開始/結束時間造成之後查詢邏輯混亂
                if (row.IsOpen)
                {
                    wrap.OpenTime = row.OpenTime;
                    wrap.CloseTime = row.CloseTime;
                }
                else
                {
                    wrap.OpenTime = null;
                    wrap.CloseTime = null;
                }

                wraps.Add(wrap);
            }

            WeekBusinessHourFactory.EditAll(wraps);
            TempData["VenueSuccessMessage"] = "營業時間設定已儲存";

            return RedirectToAction("WeekBusinessHourIndex");
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
        [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager,RoleNames.Staff)]
        public IActionResult VenueUnavailableSlotManage(int venueId, DateOnly? date)
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);

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
            TimeOnly now = TimeOnly.FromDateTime(DateTime.Now);

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
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            TimeOnly now = TimeOnly.FromDateTime(DateTime.Now);

            //防呆 >> 不能對過去的日期時間做切換,不管前端有沒有正確把按鈕disable掉
            if (date < today || (date == today && time <= now))
            {
                TempData["VenueErrorMessage"] = "已經過去的時段無法設定";
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
                wrap.CreatedAt = DateTime.Now;
                wrap.CreatedBy = 1; //尚未整合會員權限,先設為1

                VenueUnavailableSlotFactory.Create(wrap);
            }

            return RedirectToAction("VenueUnavailableSlotManage", new { venueId, date });
        }


        //整點檢查 >> null 視為合法(代表不分尖峰/離峰),非null時 分鐘/秒數必須是0
        //Create/Edit 兩個 Action 共用同一份檢查邏輯
        private bool IsWholeHour(TimeOnly? time)
        {
            if (!time.HasValue)
                return true;

            return time.Value.Minute == 0 && time.Value.Second == 0;
        }

        //營業時間範圍檢查 >> null 視為合法(代表不分尖峰/離峰),
        //非null時必須落在 [開始營業時間, 打烊前一小時] 這個範圍內(含頭含尾)
        //Create/Edit 兩個 Action 共用同一份檢查邏輯
        //⚠️【暫時寫死,待接續開發】範圍依據 CSportTypePriceRuleFactory 裡寫死的營業時間常數,
        //還沒有真正串接 WeekBusinessHour(場館營業時間管理),等那個功能做完要回來改成查表
        private bool IsWithinBusinessHours(TimeOnly? time)
        {
            if (!time.HasValue)
                return true;

            return time.Value >= CSportTypePriceRuleFactory.BusinessOpenTime
                && time.Value <= CSportTypePriceRuleFactory.LatestPeakStartTime;
        }

    }
}