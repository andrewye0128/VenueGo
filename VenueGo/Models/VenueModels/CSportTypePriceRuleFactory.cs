using Microsoft.AspNetCore.Mvc.Rendering;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.ViewModels.VenueViewModels;



namespace VenueGo.Models.VenueModels
{
    public class CSportTypePriceRuleFactory
    {
        //價格規則查詢 >> 給 SportTypePriceRuleIndex 顯示用,連帶查出運動類型名稱、每天尖峰時間的摘要與明細
        //整個方法只查 3 次DB(價格規則+運動類型、全部尖峰時間、營業時間),不在迴圈裡逐筆查詢
        public List<SportTypePriceRuleIndexViewModel> QueryAll()
        {
            List<SportTypePriceRuleIndexViewModel> list = new List<SportTypePriceRuleIndexViewModel>();

            //營業時間 7 筆,每一筆價格規則的摘要、明細都要用到,先查一次重複使用
            List<CWeekBusinessHourWrap> businessHours = new CWeekBusinessHourFactory().QueryAll();

            using (dbVenueContext db = new dbVenueContext())
            {
                //一次查出所有運動類型的尖峰時間,下面再依 SportTypeId 分給各筆價格規則
                List<SportTypePeakHour> allPeakHours = db.SportTypePeakHours.ToList();

                //LINQ查詢 >> 依SportTypeId關聯SportTypePriceRule與SportType兩張表
                var datas = from p in db.SportTypePriceRules
                            join t in db.SportTypes on p.SportTypeId equals t.SportTypeId
                            select new { PriceRule = p, SportType = t };

                //對查詢結果逐項轉成畫面要用的 ViewModel，放入 list
                foreach (var data in datas)
                {
                    SportTypePriceRuleIndexViewModel item = new SportTypePriceRuleIndexViewModel();
                    item.SportTypePriceRuleId = data.PriceRule.SportTypePriceRuleId;
                    item.SportTypeId = data.PriceRule.SportTypeId;
                    item.SportName = data.SportType.SportName;
                    item.PeakPrice = data.PriceRule.PeakPrice;
                    item.OffPeakPrice = data.PriceRule.OffPeakPrice;
                    item.IsActive = data.PriceRule.IsActive;

                    //從全部尖峰時間中挑出這個運動類型的那幾筆,包成 Wrap
                    List<CSportTypePeakHourWrap> peakHours = new List<CSportTypePeakHourWrap>();
                    foreach (SportTypePeakHour peakHour in allPeakHours)
                    {
                        if (peakHour.SportTypeId == data.PriceRule.SportTypeId)
                        {
                            CSportTypePeakHourWrap wrap = new CSportTypePeakHourWrap();
                            wrap.sportTypePeakHour = peakHour;
                            peakHours.Add(wrap);
                        }
                    }

                    //一筆都沒有 = 改版前的舊價格規則,還沒在後台儲存過,列表要提醒「尚未設定尖峰時段」
                    item.IsPeakNotConfigured = peakHours.Count == 0;

                    //7 天明細;列表不需要舊欄位當預設值(沒設定就是沒設定),所以第三個參數傳 null
                    item.Days = BuildPeakRows(peakHours, businessHours, null);
                    item.PeakSummary = BuildPeakSummary(peakHours, businessHours);

                    //有沒有尖峰、有沒有超出營業時間,都只看營業日(公休日存檔時一律是 null,不列入判斷)
                    foreach (SportTypePeakHourRowViewModel day in item.Days)
                    {
                        if (day.IsBusinessDay && day.PeakStartTime.HasValue)
                        {
                            item.HasAnyPeak = true;
                        }

                        if (day.IsOutOfRange)
                        {
                            item.HasOutOfRange = true;
                        }
                    }

                    list.Add(item);
                }
            }

            return list;
        }


        //撈出尚未設定價格規則的啟用中運動類型,供新增頁下拉選單使用
        public List<SelectListItem> GetAvailableSportTypes()
        {
            List<SelectListItem> list = new List<SelectListItem>();

            using (dbVenueContext db = new dbVenueContext())
            {
                //已經有價格規則的SportTypeId清單(不分IsActive,因為唯一索引不分IsActive)
                var usedSportTypeIds = db.SportTypePriceRules.Select(p => p.SportTypeId);

                //LINQ查詢 >> 篩選啟用中且尚未設定過價格規則的運動類型
                var datas = from t in db.SportTypes
                            where t.IsActive == true && !usedSportTypeIds.Contains(t.SportTypeId)
                            select t;

                //逐項轉成下拉選單選項,放入list
                foreach (var data in datas)
                {
                    list.Add(new SelectListItem
                    {
                        Text = data.SportName,
                        Value = data.SportTypeId.ToString()
                    });
                }
            }

            return list;
        }


        //依id查詢單筆價格規則 >> 給 SportTypePriceRuleEdit 頁面顯示用,連帶查出運動類型名稱
        public SportTypePriceRuleEditViewModel QueryById(int id)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                //LINQ查詢 >> 依SportTypeId關聯SportTypePriceRule與SportType兩張表,只取id符合的那一筆
                var data = (from p in db.SportTypePriceRules
                            join t in db.SportTypes on p.SportTypeId equals t.SportTypeId
                            where p.SportTypePriceRuleId == id
                            select new { PriceRule = p, SportType = t }).FirstOrDefault();

                if (data == null)
                    return null;

                //轉成畫面要用的 ViewModel
                SportTypePriceRuleEditViewModel vm = new SportTypePriceRuleEditViewModel();
                vm.SportTypePriceRuleId = data.PriceRule.SportTypePriceRuleId;
                vm.SportTypeId = data.PriceRule.SportTypeId;
                vm.SportTypeName = data.SportType.SportName;
                vm.PeakPrice = data.PriceRule.PeakPrice;
                vm.OffPeakPrice = data.PriceRule.OffPeakPrice;
                vm.IsActive = data.PriceRule.IsActive;

                //7 天尖峰時間:查出這個運動類型目前的每日資料,組成 7 列
                //還沒有每日資料(改版前的舊價格規則)時,營業日先用舊欄位的尖峰起始時間當預設值,
                //讓編輯畫面不是一片空白;按下儲存後 Edit() 會一次建立 7 筆
                List<CSportTypePeakHourWrap> peakHours = QueryPeakHours(data.PriceRule.SportTypeId);
                List<CWeekBusinessHourWrap> businessHours = new CWeekBusinessHourFactory().QueryAll();
                vm.Days = BuildPeakRows(peakHours, businessHours, data.PriceRule.PeakStartTime);

                return vm;
            }
        }


        //防呆用 >> 檢查該運動類型是否已經有價格規則(避免違反唯一索引)
        public bool HasPriceRule(int sportTypeId)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                return db.SportTypePriceRules.Any(p => p.SportTypeId == sportTypeId);
            }
        }

        //防呆用 >> 檢查該運動類型底下是否還有場地在使用,避免硬刪除價格規則後場地找不到對應價格
        //只算「存在的場地」(IsActive == true):Venues 的 IsActive 是軟刪除用的系統欄位,已刪除的場地不算連動
        //判斷規則跟刪除運動類型共用同一個方法(CVenueFactory.QueryActiveVenueNamesBySportType),兩邊不會不一致
        public bool HasLinkedVenues(int sportTypeId)
        {
            List<string> activeVenueNames = new CVenueFactory().QueryActiveVenueNamesBySportType(sportTypeId);
            return activeVenueNames.Count > 0;
        }

        //對外方法一 >> 依場地與預約的多個時段區塊,計算總價格
        //每個時段代表一小時,呼叫方(例如預約模組)傳入該場地所有預約區塊的起始時間清單(時段不重複,前端已保證)
        //場地不存在(含已軟刪除)或查無價格規則時會拋出KeyNotFoundException,呼叫方須自行try-catch
        public int GetPrice(int venueId, List<TimeSpan> reservationTimes)
        {
            //空清單直接回傳0,不需要為了0筆資料去查DB
            if (reservationTimes == null || reservationTimes.Count == 0)
                return 0;

            //查一次價格規則,共用私有方法,查不到會直接throw,不用try-catch(讓例外往外拋給呼叫方處理)
            CSportTypePriceRuleWrap rule = GetPriceRuleForVenue(venueId);

            int total = 0;

            foreach (var time in reservationTimes)
            {
                //判斷這個時段是否落在尖峰 >> PeakStartTime有值,且time >= PeakStartTime(含起始點)才算尖峰
                bool isPeak;
                if (rule.PeakStartTime.HasValue && time >= rule.PeakStartTime.Value.ToTimeSpan())
                {
                    isPeak = true;
                }
                else
                {
                    isPeak = false;
                }

                //依判斷結果加總對應價格
                if (isPeak)
                {
                    total += rule.PeakPrice;
                }
                else
                {
                    total += rule.OffPeakPrice;
                }
            }

            return total;
        }


        //對外方法二 >> 依場地查出尖峰/離峰價格,供顯示用途(例如場地卡片),不做時段判斷或加總
        //場地不存在(含已經軟刪除的)或查無價格規則時會拋出KeyNotFoundException,呼叫方須自行try-catch接住
        public VenuePriceInfo GetPriceRule(int venueId)
        {
            CSportTypePriceRuleWrap rule = GetPriceRuleForVenue(venueId);

            VenuePriceInfo vm = new VenuePriceInfo();

            //若該運動類型不分尖峰離峰,PeakPrice刻意回傳null,即使DB裡有存數字,避免呼叫方誤用成「這個場地有尖峰價」
            if (rule.PeakStartTime.HasValue)
            {
                vm.PeakPrice = rule.PeakPrice;
            }
            else
            {
                vm.PeakPrice = null;
            }

            vm.OffPeakPrice = rule.OffPeakPrice;

            return vm;
        }


        //共用私有方法 >> 查場地(過濾IsActive) → 查價格規則(過濾IsActive) → 回傳
        //查不到任一者皆拋出KeyNotFoundException,GetPrice()與GetPriceRule()皆呼叫此方法,避免查詢邏輯重複
        private CSportTypePriceRuleWrap GetPriceRuleForVenue(int venueId)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                //先查場地,必須是啟用中,查不到就代表場地不存在或已軟刪除
                var venue = db.Venues.FirstOrDefault(v => v.VenueId == venueId && v.IsActive);
                if (venue == null)
                {
                    throw new KeyNotFoundException($"場地不存在或已停用(VenueId={venueId})");
                }

                //再用場地的SportTypeId查價格規則,必須是啟用中
                var priceRule = db.SportTypePriceRules.FirstOrDefault(p => p.SportTypeId == venue.SportTypeId && p.IsActive);
                if (priceRule == null)
                {
                    throw new KeyNotFoundException($"此運動類型尚未設定價格規則或已停用(SportTypeId={venue.SportTypeId})");
                }

                //包成Wrap回傳,離開using區塊後DbContext釋放也沒關係,因為資料已經materialize到記憶體
                CSportTypePriceRuleWrap wrap = new CSportTypePriceRuleWrap();
                wrap.sportTypePriceRule = priceRule;
                return wrap;
            }
        }


        //價格規則刪除 >> 真正的硬刪除(不同於SportType/Venue的軟刪除),
        //呼叫前Controller必須先用HasLinkedVenues確認沒有場地連動
        //連同這個運動類型在 SportTypePeakHours 的 7 筆尖峰時間一起刪除,
        //兩張表在同一個 DbContext 裡處理、最後只 SaveChanges 一次,避免只刪掉其中一邊留下孤兒資料
        public void Delete(int id)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                var data = db.SportTypePriceRules.FirstOrDefault(p => p.SportTypePriceRuleId == id);
                if (data != null)
                {
                    //先找出同一個運動類型的尖峰時間(正常是 7 筆,舊資料可能是 0 筆),逐筆標記刪除
                    var peakHours = db.SportTypePeakHours.Where(p => p.SportTypeId == data.SportTypeId).ToList();
                    foreach (var peakHour in peakHours)
                    {
                        db.SportTypePeakHours.Remove(peakHour);
                    }

                    db.SportTypePriceRules.Remove(data);
                    db.SaveChanges();
                }
            }
        }


        /***** 每天各自設定尖峰起始時間(SportTypePeakHours) *****/

        //畫面顯示用的星期順序 >> 星期一排最前面、星期日排最後面(跟營業時間管理頁一致)
        //System.DayOfWeek 本身是星期日=0 排最前面,所以另外列一份固定順序,需要照畫面順序跑迴圈時用這份
        private static readonly DayOfWeek[] DisplayOrderDays = new DayOfWeek[]
        {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday,
            DayOfWeek.Saturday,
            DayOfWeek.Sunday
        };


        //查詢某個運動類型 7 天的尖峰起始時間,依 DayOfWeek 0~6(星期日~星期六)排序
        //回傳 0 筆 >> 代表這個運動類型還沒設定過每日尖峰時間(改版前就存在的舊價格規則),由呼叫方決定怎麼處理
        //回傳 7 筆 >> 正常情況
        public List<CSportTypePeakHourWrap> QueryPeakHours(int sportTypeId)
        {
            List<CSportTypePeakHourWrap> list = new List<CSportTypePeakHourWrap>();

            using (dbVenueContext db = new dbVenueContext())
            {
                var datas = from p in db.SportTypePeakHours
                            where p.SportTypeId == sportTypeId
                            orderby p.DayOfWeek
                            select p;

                foreach (var data in datas)
                {
                    CSportTypePeakHourWrap wrap = new CSportTypePeakHourWrap();
                    wrap.sportTypePeakHour = data;
                    list.Add(wrap);
                }
            }

            return list;
        }


        //某一天的尖峰起始時間下拉選單選項 >> 從當天開始營業時間到「當天打烊前一小時」,每小時一個選項
        //營業時間由呼叫方傳入(不在這裡查DB):Controller 查一次 7 天營業時間後,重複傳進來用,避免同一個頁面查 7 次DB
        //公休日、或查不到那天的營業時間(businessHour 是 null) >> 回傳空清單
        //「不分尖峰/離峰」選項不放在這裡,由 View 自己加在最前面(跟原本的做法一樣)
        public List<SelectListItem> GetPeakStartTimeOptions(CWeekBusinessHourWrap? businessHour)
        {
            List<SelectListItem> list = new List<SelectListItem>();

            //公休日或資料不完整,沒有任何合法的尖峰起始時間
            if (businessHour == null || !businessHour.IsOpen || !businessHour.OpenTime.HasValue || !businessHour.CloseTime.HasValue)
            {
                return list;
            }

            int firstHour = businessHour.OpenTime.Value.Hour;
            //尖峰最晚要在打烊前一小時開始,讓尖峰時段至少有一小時(一個預約格)
            int lastHour = businessHour.CloseTime.Value.Hour - 1;

            //用int(小時數)控制迴圈,不能直接用TimeOnly本身遞增比較:
            //TimeOnly沒有24:00這個值,23:00.AddHours(1)會繞回00:00,迴圈條件永遠成立,造成無窮迴圈(第七節踩過的坑)
            for (int hour = firstHour; hour <= lastHour; hour++)
            {
                string text = new TimeOnly(hour, 0).ToString("HH:mm");
                list.Add(new SelectListItem { Text = text, Value = text });
            }

            return list;
        }


        //判斷某一天的尖峰起始時間是否合法 >> 後端驗證、列表的「超出營業時間」提示、編輯頁的橘色標示,全部共用這一個方法,
        //確保「畫面上選得到的」跟「存檔時允許的」永遠是同一套規則
        //營業時間由呼叫方傳入(不在這裡查DB),理由同 GetPeakStartTimeOptions
        public bool IsPeakStartTimeValid(TimeOnly? peakStartTime, CWeekBusinessHourWrap? businessHour)
        {
            //沒有設定尖峰時間 = 這天不分尖峰/離峰,一定合法(公休日也是存 null)
            if (!peakStartTime.HasValue)
            {
                return true;
            }

            //有設定尖峰時間,但那天公休或營業時間資料不完整 >> 不合法
            if (businessHour == null || !businessHour.IsOpen || !businessHour.OpenTime.HasValue || !businessHour.CloseTime.HasValue)
            {
                return false;
            }

            TimeOnly time = peakStartTime.Value;

            //只能是整點(預約以一小時為一格,避免同一格被切成一半尖峰一半離峰)
            if (time.Minute != 0 || time.Second != 0)
            {
                return false;
            }

            //必須介於「開始營業」~「打烊前一小時」之間(含頭含尾),跟下拉選單產生的範圍一致
            TimeOnly earliest = businessHour.OpenTime.Value;
            TimeOnly latest = businessHour.CloseTime.Value.AddHours(-1);

            if (time >= earliest && time <= latest)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        //產生價格規則列表用的「尖峰時段」摘要文字 >> 例如「每天 17:00」、「週二~週五 17:00、週六~週日 15:00」
        //規則(2026-09-28 HungYu 定案):
        //1. 公休日跳過:不列出,也不會切斷前後的連續(週三公休時,週二跟週四視為相連)
        //2. 營業但不分尖峰/離峰的日子不列出,但會切斷連續
        //3. 所有營業日的尖峰時間都相同 >> 「每天 HH:mm」
        //4. 連續幾天相同 >> 「週X~週Y HH:mm」;只有一天 >> 「週X HH:mm」
        //5. 多段之間用頓號「、」串接
        //6. 順序是星期一排最前、星期日排最後,所以星期日不會跟星期一接成一段
        //所有營業日都沒有尖峰(或根本沒有營業日) >> 回傳空字串,由 View 顯示「不分尖/離峰」
        //營業時間由呼叫方傳入(不在這裡查DB),列表頁只要查一次營業時間就能套用到每一筆價格規則
        public string BuildPeakSummary(List<CSportTypePeakHourWrap> peakHours, List<CWeekBusinessHourWrap> businessHours)
        {
            //第一步:照畫面順序(週一~週日)整理出「營業日」跟「那天的尖峰時間」兩份等長的清單,公休日直接跳過
            List<DayOfWeek> days = new List<DayOfWeek>();
            List<TimeOnly?> times = new List<TimeOnly?>();

            foreach (DayOfWeek day in DisplayOrderDays)
            {
                CWeekBusinessHourWrap? businessHour = FindBusinessHour(businessHours, day);
                if (businessHour == null || !businessHour.IsOpen)
                {
                    continue;
                }

                days.Add(day);
                times.Add(FindPeakStartTime(peakHours, day));
            }

            //第二步:所有營業日都沒有尖峰 >> 回傳空字串
            bool hasAnyPeak = false;
            foreach (TimeOnly? time in times)
            {
                if (time.HasValue)
                {
                    hasAnyPeak = true;
                }
            }

            if (!hasAnyPeak)
            {
                return string.Empty;
            }

            //第三步:所有營業日的尖峰時間都一樣(而且都有值) >> 「每天 HH:mm」
            //先把第一天的值放進區域變數,編譯器才能確認 HasValue 檢查過後 .Value 不會是 null
            TimeOnly? firstTime = times[0];
            bool isAllSame = true;
            foreach (TimeOnly? time in times)
            {
                if (time != firstTime)
                {
                    isAllSame = false;
                }
            }

            if (isAllSame && firstTime.HasValue)
            {
                return "每天 " + firstTime.Value.ToString("HH:mm");
            }

            //第四步:把「連續、時間相同」的日子合併成一段
            //start 是目前這一段的第一天在清單中的位置;往後掃,遇到時間不同(或掃到最後)就結算這一段
            List<string> parts = new List<string>();
            int start = 0;

            for (int i = 1; i <= days.Count; i++)
            {
                //i 等於 days.Count 代表已經掃完,最後一段也要結算
                bool isSegmentEnd;
                if (i == days.Count)
                {
                    isSegmentEnd = true;
                }
                else if (times[i] != times[start])
                {
                    isSegmentEnd = true;
                }
                else
                {
                    isSegmentEnd = false;
                }

                if (isSegmentEnd)
                {
                    //這一段是「不分尖峰/離峰」的日子就不列出
                    //一樣先放進區域變數,理由同第三步
                    TimeOnly? segmentTime = times[start];
                    if (segmentTime.HasValue)
                    {
                        int end = i - 1;
                        string dayText;
                        if (start == end)
                        {
                            dayText = GetShortDayName(days[start]);
                        }
                        else
                        {
                            dayText = GetShortDayName(days[start]) + "~" + GetShortDayName(days[end]);
                        }

                        parts.Add(dayText + " " + segmentTime.Value.ToString("HH:mm"));
                    }

                    //下一段從目前位置開始
                    start = i;
                }
            }

            return string.Join("、", parts);
        }


        //把尖峰時間跟營業時間組成畫面用的 7 列(週一~週日) >> 新增/編輯表單的 7 列表格、列表頁的明細共用
        //peakHours:這個運動類型的尖峰時間(可以是 0 筆、部分天數、7 筆)
        //businessHours:7 天營業時間(由呼叫方查好傳入,不在這裡查DB)
        //legacyPeakStartTime:舊欄位 SportTypePriceRules.PeakStartTime,只有「peakHours 一筆都沒有」時才拿來當營業日的預設值;
        //                     新增頁、列表頁不需要預設值,傳 null
        //每一列的規則:
        //1. 公休日 >> 不給選項、尖峰時間顯示為 null(存檔時 Controller 也會強制清成 null)
        //2. 營業日 >> 選項 = 當天開始營業 ~ 打烊前一小時的整點
        //3. 營業日的值超出當天營業時間(營業時間之後被改過) >> IsOutOfRange = true,
        //   並把這個值額外加在選項最前面,標示「(超出營業時間)」:
        //   讓使用者看得到原本設了什麼,不會因為選項裡沒有這個值而變成空白、存檔時被悄悄清掉;
        //   這個值存檔時會被 IsPeakStartTimeValid 擋下,使用者必須改成範圍內的時間或不分尖峰/離峰
        public List<SportTypePeakHourRowViewModel> BuildPeakRows(List<CSportTypePeakHourWrap> peakHours, List<CWeekBusinessHourWrap> businessHours, TimeOnly? legacyPeakStartTime)
        {
            List<SportTypePeakHourRowViewModel> rows = new List<SportTypePeakHourRowViewModel>();

            //有沒有已經存過的每日資料;一筆都沒有時才使用舊欄位當預設值
            bool hasSavedData = peakHours.Count > 0;

            foreach (DayOfWeek day in DisplayOrderDays)
            {
                SportTypePeakHourRowViewModel row = new SportTypePeakHourRowViewModel();
                row.DayOfWeek = day;
                row.DayName = GetShortDayName(day);

                //這天是否營業:查不到營業時間、IsOpen=false、或時間不完整,都當作公休
                CWeekBusinessHourWrap? businessHour = FindBusinessHour(businessHours, day);
                if (businessHour != null && businessHour.IsOpen && businessHour.OpenTime.HasValue && businessHour.CloseTime.HasValue)
                {
                    row.IsBusinessDay = true;
                    row.OpenTime = businessHour.OpenTime;
                    row.CloseTime = businessHour.CloseTime;
                }
                else
                {
                    row.IsBusinessDay = false;
                }

                //這天要顯示的尖峰時間:有存過的資料就用存的值(那天沒有資料就是 null),都沒存過才用舊欄位
                TimeOnly? value;
                if (hasSavedData)
                {
                    value = FindPeakStartTime(peakHours, day);
                }
                else
                {
                    value = legacyPeakStartTime;
                }

                if (!row.IsBusinessDay)
                {
                    //公休日:不能設定尖峰,畫面上一律顯示為不分尖峰/離峰,也不需要選項
                    row.PeakStartTime = null;
                    row.IsOutOfRange = false;
                }
                else
                {
                    row.PeakStartTime = value;
                    row.Options = GetPeakStartTimeOptions(businessHour);
                    row.IsOutOfRange = !IsPeakStartTimeValid(value, businessHour);

                    //超出範圍的舊值補在選項最前面,讓下拉選單能顯示它(規則 3)
                    if (row.IsOutOfRange && value.HasValue)
                    {
                        string text = value.Value.ToString("HH:mm");
                        row.Options.Insert(0, new SelectListItem { Text = text + "（超出營業時間）", Value = text });
                    }
                }

                rows.Add(row);
            }

            return rows;
        }


        //場館公休日的文字,例如「週一」或「週一、週三」(週一排最前面),沒有公休日時回傳空字串
        //價格規則列表頁、新增/編輯頁頂端的「場館公休日」提醒使用
        //營業時間由呼叫方查好傳入(不在這裡查DB)
        public string BuildClosedDaysText(List<CWeekBusinessHourWrap> businessHours)
        {
            List<string> closedDays = new List<string>();

            foreach (DayOfWeek day in DisplayOrderDays)
            {
                //判斷規則跟 BuildPeakRows 一致:查不到營業時間或 IsOpen=false 都算公休
                CWeekBusinessHourWrap? businessHour = FindBusinessHour(businessHours, day);
                if (businessHour == null || !businessHour.IsOpen)
                {
                    closedDays.Add(GetShortDayName(day));
                }
            }

            return string.Join("、", closedDays);
        }


        //價格規則新增(每天各自設定尖峰時間版本)
        //rule:價格規則本身;peakHours:7 天的尖峰時間(公休日應由 Controller 先強制清成 null)
        //userId / now 由 Controller 傳入(登入者 UserId、ITimeService 校時後的時間)
        //價格規則跟 7 筆尖峰時間在同一個 DbContext 裡新增,最後只 SaveChanges 一次:
        //兩邊要嘛一起成功、要嘛一起失敗,不會出現「價格規則存進去了,尖峰時間沒存到」的半套資料
        //注意:舊欄位 SportTypePriceRules.PeakStartTime 刻意不寫入(2026-09-27 決定舊欄位不同步),
        //新建的價格規則在舊欄位上會是 null;預約模組改成呼叫本模組的方法後,這個舊欄位就會刪除
        public void Create(CSportTypePriceRuleWrap rule, List<CSportTypePeakHourWrap> peakHours, int userId, DateTime now)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                //價格規則的稽核欄位:這張表沒有 CreatedAt/By,新增也算一次異動,寫入 UpdatedAt/By 保留建立者資訊
                rule.UpdatedAt = now;
                rule.UpdatedBy = userId;
                db.SportTypePriceRules.Add(rule.sportTypePriceRule);

                //7 筆尖峰時間全部寫入稽核欄位(包含不分尖峰/離峰與公休的日子),確保這張表不會出現稽核欄位是 null 的資料
                //SportTypeId 以價格規則為準,不相信 peakHours 裡原本帶的值
                foreach (CSportTypePeakHourWrap peakHour in peakHours)
                {
                    peakHour.SportTypeId = rule.SportTypeId;
                    peakHour.UpdatedAt = now;
                    peakHour.UpdatedBy = userId;
                    db.SportTypePeakHours.Add(peakHour.sportTypePeakHour);
                }

                //同一天重複兩筆時,資料庫的唯一索引(SportTypeId + DayOfWeek)會讓整批失敗,不會寫進半套
                db.SaveChanges();
            }
        }


        //價格規則修改(每天各自設定尖峰時間版本,含 IsActive 停用/啟用)
        //rule:畫面送回的價格規則;peakHours:7 天的尖峰時間(公休日應由 Controller 先強制清成 null)
        //userId / now 由 Controller 傳入(登入者 UserId、ITimeService 校時後的時間)
        //尖峰時間逐天處理:
        //1. 那天在資料庫還沒有資料(改版前的舊價格規則,或只有部分天數) >> 新增,並寫入稽核欄位
        //2. 那天已有資料而且值有變 >> 更新,並寫入稽核欄位
        //3. 那天已有資料而且值沒變 >> 不動,保留原本的稽核戳記,這樣每一天的 UpdatedAt 才看得出「這一天」最後是誰、何時改的
        //舊價格規則(0 筆)只要在後台儲存一次,就會補齊 7 筆
        public void Edit(CSportTypePriceRuleWrap rule, List<CSportTypePeakHourWrap> peakHours, int userId, DateTime now)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                //依照Wrap傳來的id查找對應資料,查不到就什麼都不做
                var data = db.SportTypePriceRules.FirstOrDefault(p => p.SportTypePriceRuleId == rule.SportTypePriceRuleId);
                if (data == null)
                {
                    return;
                }

                //價格規則本身:SportTypeId 不開放編輯,故意不覆蓋;
                //舊欄位 PeakStartTime 也刻意不動(舊欄位不同步,理由同 Create)
                data.PeakPrice = rule.PeakPrice;
                data.OffPeakPrice = rule.OffPeakPrice;
                data.IsActive = rule.IsActive;

                //稽核欄位:記錄最後修改時間與修改者
                data.UpdatedAt = now;
                data.UpdatedBy = userId;

                //一次查出這個運動類型目前在資料庫的所有尖峰時間,下面逐天比對時就不用每天各查一次DB
                //SportTypeId 以資料庫裡的價格規則為準,不相信畫面送回來的值
                List<SportTypePeakHour> existingPeakHours = db.SportTypePeakHours.Where(p => p.SportTypeId == data.SportTypeId).ToList();

                foreach (CSportTypePeakHourWrap peakHour in peakHours)
                {
                    //在資料庫的資料中找出同一天的那一筆
                    byte dayValue = (byte)peakHour.DayOfWeek;
                    SportTypePeakHour? existing = null;
                    foreach (SportTypePeakHour item in existingPeakHours)
                    {
                        if (item.DayOfWeek == dayValue)
                        {
                            existing = item;
                        }
                    }

                    if (existing == null)
                    {
                        //情況1:那天還沒有資料 >> 新增
                        peakHour.SportTypeId = data.SportTypeId;
                        peakHour.UpdatedAt = now;
                        peakHour.UpdatedBy = userId;
                        db.SportTypePeakHours.Add(peakHour.sportTypePeakHour);
                    }
                    else if (existing.PeakStartTime != peakHour.PeakStartTime)
                    {
                        //情況2:值有變 >> 更新
                        existing.PeakStartTime = peakHour.PeakStartTime;
                        existing.UpdatedAt = now;
                        existing.UpdatedBy = userId;
                    }
                    //情況3:值沒變 >> 不動
                }

                //價格規則跟尖峰時間在同一個 DbContext 裡,最後只 SaveChanges 一次,要嘛一起成功、要嘛一起失敗
                db.SaveChanges();
            }
        }


        //從營業時間清單中找出某一天的那一筆,找不到回傳 null
        private CWeekBusinessHourWrap? FindBusinessHour(List<CWeekBusinessHourWrap> businessHours, DayOfWeek day)
        {
            foreach (CWeekBusinessHourWrap businessHour in businessHours)
            {
                if (businessHour.DayOfWeek == day)
                {
                    return businessHour;
                }
            }

            return null;
        }


        //從尖峰時間清單中找出某一天的尖峰起始時間,找不到(還沒設定)或那天不分尖峰/離峰都回傳 null
        private TimeOnly? FindPeakStartTime(List<CSportTypePeakHourWrap> peakHours, DayOfWeek day)
        {
            foreach (CSportTypePeakHourWrap peakHour in peakHours)
            {
                if (peakHour.DayOfWeek == day)
                {
                    return peakHour.PeakStartTime;
                }
            }

            return null;
        }


        //星期的簡短名稱 >> 摘要文字、7 列表格、錯誤訊息都用「週一」格式
        //Controller 的 GetDayName() 回傳的是「星期一」格式(營業時間頁用),格式不同所以另外寫一個
        //public:Controller 整理表單送回的 7 列時也要用(NormalizePeakDays)
        public string GetShortDayName(DayOfWeek day)
        {
            string name;

            if (day == DayOfWeek.Monday)
            {
                name = "週一";
            }
            else if (day == DayOfWeek.Tuesday)
            {
                name = "週二";
            }
            else if (day == DayOfWeek.Wednesday)
            {
                name = "週三";
            }
            else if (day == DayOfWeek.Thursday)
            {
                name = "週四";
            }
            else if (day == DayOfWeek.Friday)
            {
                name = "週五";
            }
            else if (day == DayOfWeek.Saturday)
            {
                name = "週六";
            }
            else
            {
                name = "週日";
            }

            return name;
        }
    }
}