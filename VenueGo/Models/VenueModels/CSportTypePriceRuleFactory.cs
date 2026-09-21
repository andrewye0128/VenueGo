using Microsoft.AspNetCore.Mvc.Rendering;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.ViewModels.VenueViewModels;



namespace VenueGo.Models.VenueModels
{
    public class CSportTypePriceRuleFactory
    {
        // ⚠️【暫時寫死,待接續開發】營業時間目前還沒有 WeekBusinessHour 功能可以查,
        // 先假設全週營業時間固定是 10:00~22:00。
        // TODO: 等 WeekBusinessHour(場館營業時間管理)開發完成後,
        //       這兩個值要改成向 WeekBusinessHour 資料表查詢實際營業時間,
        //       不能再繼續用這裡寫死的常數。
        public static readonly TimeOnly BusinessOpenTime = new TimeOnly(10, 0);   //開始營業時間
        public static readonly TimeOnly BusinessCloseTime = new TimeOnly(22, 0);  //結束營業時間(打烊時間)

        //尖峰起始時間最晚可以設定到「打烊前一小時」,讓尖峰時段至少有一小時長度,
        //所以不是直接用 BusinessCloseTime,而是再往前推一小時
        public static readonly TimeOnly LatestPeakStartTime = BusinessCloseTime.AddHours(-1);



        //價格規則查詢 >> 給 SportTypePriceRuleIndex 顯示用,連帶查出運動類型名稱
        public List<SportTypePriceRuleIndexViewModel> QueryAll()
        {
            List<SportTypePriceRuleIndexViewModel> list = new List<SportTypePriceRuleIndexViewModel>();

            using (dbVenueContext db = new dbVenueContext())
            {
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
                    item.PeakStartTime = data.PriceRule.PeakStartTime;
                    item.PeakPrice = data.PriceRule.PeakPrice;
                    item.OffPeakPrice = data.PriceRule.OffPeakPrice;
                    item.IsActive = data.PriceRule.IsActive;
                    list.Add(item);
                }
            }

            return list;
        }


        //尖峰起始時間下拉選單選項 >> 從開始營業時間到「打烊前一小時」,每小時一個選項
        //改用下拉選單(而不是 <input type="time">)是為了在 UI 上直接讓使用者只選得出整點,
        //而不是靠 step/min/max 這種「使用者還是打得出不合法值,只能靠瀏覽器提示」的做法
        //⚠️【暫時寫死,待接續開發】選項範圍依據上面寫死的營業時間常數產生,
        //等 WeekBusinessHour(場館營業時間管理)開發完成後,要改成依照實際營業時間動態產生選項
        public List<SelectListItem> GetPeakStartTimeOptions()
        {
            List<SelectListItem> list = new List<SelectListItem>();

            for (TimeOnly time = BusinessOpenTime; time <= LatestPeakStartTime; time = time.AddHours(1))
            {
                string text = time.ToString("HH:mm");
                list.Add(new SelectListItem { Text = text, Value = text });
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
                vm.PeakStartTime = data.PriceRule.PeakStartTime;
                vm.PeakPrice = data.PriceRule.PeakPrice;
                vm.OffPeakPrice = data.PriceRule.OffPeakPrice;
                vm.IsActive = data.PriceRule.IsActive;
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

        //價格規則新增
        public void Create(CSportTypePriceRuleWrap Wrap)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                db.SportTypePriceRules.Add(Wrap.sportTypePriceRule);
                db.SaveChanges();
            }
        }


        //價格規則修改
        public void Edit(CSportTypePriceRuleWrap Wrap)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                //依照Wrap傳來的id查找對應資料
                var data = db.SportTypePriceRules.FirstOrDefault(p => p.SportTypePriceRuleId == Wrap.SportTypePriceRuleId);
                if (data != null)
                {
                    //SportTypeId 不開放編輯,故意不覆蓋,只更新以下欄位
                    data.PeakStartTime = Wrap.PeakStartTime;
                    data.PeakPrice = Wrap.PeakPrice;
                    data.OffPeakPrice = Wrap.OffPeakPrice;
                    data.IsActive = Wrap.IsActive;
                }

                db.SaveChanges();
            }
        }


        //防呆用 >> 檢查該運動類型底下是否還有場地在使用,避免硬刪除價格規則後場地找不到對應價格
        //不分場地IsActive,因為停用中的場地之後可能重新啟用,一樣需要有價格規則
        public bool HasLinkedVenues(int sportTypeId)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                return db.Venues.Any(v => v.SportTypeId == sportTypeId);
            }
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
        public void Delete(int id)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                var data = db.SportTypePriceRules.FirstOrDefault(p => p.SportTypePriceRuleId == id);
                if (data != null)
                {
                    db.SportTypePriceRules.Remove(data);
                    db.SaveChanges();
                }
            }
        }
    }
}