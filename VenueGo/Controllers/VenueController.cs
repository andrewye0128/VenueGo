using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Net.WebSockets;
using VenueGo.Data;
using VenueGo.Models.VenueModels;
using VenueGo.ViewModels;

namespace VenueGo.Controllers
{
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

        //新增運動類型 >> 頁面產生
        public IActionResult SportTypeCreate()
        {
            return View();
        }


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
        //列出所有場地
        public IActionResult VenueIndex()
        {
            //撈出所有場地的資料
            List<CVenueWrap> datas = new CVenueFactory().QueryAll();

            //撈出運動類型表,傳到前端提供顯示運動類型分類
            var sportTypeNames = new CVenueFactory().GetSportTypes().ToDictionary(x => int.Parse(x.Value), x => x.Text);

            ViewBag.SportTypeNames = sportTypeNames;

            return View(datas);
        }

        //場地新增>> 頁面產生 
        public IActionResult VenueCreate()
        {
            //把運動類型送到前端 >> 做成下拉選單
            //用viewModel存
            var vm = new VenueCreateViewModel();
            vm.SportTypes = new CVenueFactory().GetSportTypes();
            return View(vm);
        }


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
                TempData["ErrorMessage"] = $"「{data.SportTypeName}」目前仍有場地使用中，無法刪除價格規則";
                return RedirectToAction("SportTypePriceRuleIndex");
            }

            //檢查通過,執行硬刪除
            SportTypePriceRuleFactory.Delete((int)id);
            TempData["SuccessMessage"] = $"已成功刪除「{data.SportTypeName}」的價格規則";

            return RedirectToAction("SportTypePriceRuleIndex");
        }


        /*WeekBusinessHour*/

        //場館營業時間管理 >> 頁面產生,一次顯示7天,星期一排最前面、星期日排最後面
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
            TempData["SuccessMessage"] = "營業時間設定已儲存";

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
