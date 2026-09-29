using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.Models.VenueModels;

namespace VenueGo.Services.VenueSchedules
{
    //場地時段服務的實作 >> 介面說明見 IVenueScheduleService
    //
    //寫法說明:
    //1. dbVenueContext 由 DI 注入(Scoped),同一個網頁請求裡會跟預約模組的服務共用同一個 DbContext;
    //   所以所有查詢都加 AsNoTracking():只讀不寫,不在共用的 DbContext 裡留下追蹤中的物件,
    //   避免預約模組呼叫 SaveChanges() 時,把這裡查出來的資料意外一起寫回資料庫
    //2. 每個方法一次查完需要的資料,不在迴圈裡查DB
    //3. 規則不在這裡重寫,一律呼叫場地模組 Factory 裡「不查DB」的方法,讓後台畫面跟這個服務用同一份規則:
    //   - 營業時段怎麼切      >> CWeekBusinessHourFactory.ExpandToSlots
    //   - 哪一格算尖峰        >> CSportTypePriceRuleFactory.IsPeakSlot
    //   - 尖峰時段摘要文字    >> CSportTypePriceRuleFactory.BuildPeakSummary
    //4. 只讀每日尖峰時間新表 SportTypePeakHours,不讀舊欄位 SportTypePriceRules.PeakStartTime;
    //   某個價格規則還沒有每日資料時,當作不分尖峰/離峰
    //5. 不需要「現在時間」:判斷時段是否已經過去是預約模組的責任,所以不注入 ITimeService
    public class VenueScheduleService : IVenueScheduleService
    {
        private readonly dbVenueContext _db;

        //Factory 裡「不查DB」的規則方法(營業時段切法、尖峰判斷、摘要文字)
        private readonly CWeekBusinessHourFactory _weekBusinessHourFactory = new CWeekBusinessHourFactory();
        private readonly CSportTypePriceRuleFactory _sportTypePriceRuleFactory = new CSportTypePriceRuleFactory();

        public VenueScheduleService(dbVenueContext db)
        {
            _db = db;
        }


        /***** 對外方法 *****/

        public async Task<IReadOnlyList<VenueSlotInfo>?> GetDayScheduleAsync(
            int venueId, DateOnly date, CancellationToken cancellationToken = default)
        {
            //1. 場地不能使用 >> null
            UsableVenueRow? venue = await LoadUsableVenueAsync(venueId, cancellationToken);
            if (venue == null)
            {
                return null;
            }

            //2. 營業時間(7 筆)、價格規則、每日尖峰時間
            List<CWeekBusinessHourWrap> businessHours = await LoadBusinessHoursAsync(cancellationToken);
            SportTypePriceRule? priceRule = await LoadActivePriceRuleAsync(venue.SportTypeId, cancellationToken);
            List<CSportTypePeakHourWrap> peakHours = await LoadPeakHoursAsync(venue.SportTypeId, cancellationToken);

            //3. 這天被設為不開放的時段(只需要時間,用 Select 只取這一個欄位)
            List<TimeOnly> unavailableTimes = await _db.VenueUnavailableSlots.AsNoTracking()
                .Where(u => u.VenueId == venueId && u.UnavailableDate == date)
                .Select(u => u.UnavailableTime)
                .ToListAsync(cancellationToken);

            //4. 組出這天的每一格
            return BuildDaySlots(date, businessHours, new HashSet<TimeOnly>(unavailableTimes), priceRule, peakHours);
        }


        public async Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<VenueSlotInfo>>?> GetRangeScheduleAsync(
            int venueId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
        {
            //1. 場地不能使用 >> null
            UsableVenueRow? venue = await LoadUsableVenueAsync(venueId, cancellationToken);
            if (venue == null)
            {
                return null;
            }

            Dictionary<DateOnly, IReadOnlyList<VenueSlotInfo>> result = new Dictionary<DateOnly, IReadOnlyList<VenueSlotInfo>>();

            //2. 起日晚於迄日 >> 空字典(跟預約模組原本 GetRangeAvailabilityAsync 的行為一致)
            if (fromDate > toDate)
            {
                return result;
            }

            //3. 整段期間需要的資料一次查完:營業時間 7 筆、價格規則、每日尖峰時間、期間內所有不開放時段
            List<CWeekBusinessHourWrap> businessHours = await LoadBusinessHoursAsync(cancellationToken);
            SportTypePriceRule? priceRule = await LoadActivePriceRuleAsync(venue.SportTypeId, cancellationToken);
            List<CSportTypePeakHourWrap> peakHours = await LoadPeakHoursAsync(venue.SportTypeId, cancellationToken);

            var unavailableRows = await _db.VenueUnavailableSlots.AsNoTracking()
                .Where(u => u.VenueId == venueId && u.UnavailableDate >= fromDate && u.UnavailableDate <= toDate)
                .Select(u => new { u.UnavailableDate, u.UnavailableTime })
                .ToListAsync(cancellationToken);

            //把不開放時段依日期分組,逐日組時段時直接取用
            Dictionary<DateOnly, HashSet<TimeOnly>> unavailableByDate = new Dictionary<DateOnly, HashSet<TimeOnly>>();
            foreach (var row in unavailableRows)
            {
                if (!unavailableByDate.ContainsKey(row.UnavailableDate))
                {
                    unavailableByDate[row.UnavailableDate] = new HashSet<TimeOnly>();
                }
                unavailableByDate[row.UnavailableDate].Add(row.UnavailableTime);
            }

            //4. 逐日組出時段,每一天都放進字典(公休日是空清單)
            for (DateOnly date = fromDate; date <= toDate; date = date.AddDays(1))
            {
                HashSet<TimeOnly> unavailableTimes;
                if (unavailableByDate.ContainsKey(date))
                {
                    unavailableTimes = unavailableByDate[date];
                }
                else
                {
                    unavailableTimes = new HashSet<TimeOnly>();
                }

                result[date] = BuildDaySlots(date, businessHours, unavailableTimes, priceRule, peakHours);
            }

            return result;
        }


        public async Task<UsableVenueInfo?> GetUsableVenueAsync(
            int venueId, CancellationToken cancellationToken = default)
        {
            //1. 場地不能使用 >> null
            UsableVenueRow? venue = await LoadUsableVenueAsync(venueId, cancellationToken);
            if (venue == null)
            {
                return null;
            }

            //2. 組價格摘要需要的資料
            List<CWeekBusinessHourWrap> businessHours = await LoadBusinessHoursAsync(cancellationToken);
            SportTypePriceRule? priceRule = await LoadActivePriceRuleAsync(venue.SportTypeId, cancellationToken);
            List<CSportTypePeakHourWrap> peakHours = await LoadPeakHoursAsync(venue.SportTypeId, cancellationToken);

            return ToUsableVenueInfo(venue, BuildPriceSummary(priceRule, peakHours, businessHours));
        }


        public async Task<IReadOnlyList<UsableVenueInfo>> SearchUsableVenuesAsync(
            int? sportTypeId, string? keyword, CancellationToken cancellationToken = default)
        {
            //1. 可以使用的場地:場地存在,而且它的運動類型也存在
            var query = from v in _db.Venues.AsNoTracking()
                        join s in _db.SportTypes.AsNoTracking() on v.SportTypeId equals s.SportTypeId
                        where v.IsActive == true && s.IsActive == true
                        select new UsableVenueRow
                        {
                            VenueId = v.VenueId,
                            VenueName = v.VenueName,
                            SportTypeId = v.SportTypeId,
                            SportName = s.SportName,
                            Capacity = v.Capacity,
                            PhotoPath = v.PhotoPath
                        };

            //2. 篩選條件:有給才加
            if (sportTypeId.HasValue)
            {
                query = query.Where(x => x.SportTypeId == sportTypeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string trimmedKeyword = keyword.Trim();
                query = query.Where(x => x.VenueName.Contains(trimmedKeyword));
            }

            List<UsableVenueRow> venues = await query
                .OrderBy(x => x.SportTypeId)
                .ThenBy(x => x.VenueId)
                .ToListAsync(cancellationToken);

            List<UsableVenueInfo> result = new List<UsableVenueInfo>();
            if (venues.Count == 0)
            {
                return result;
            }

            //3. 這些場地涉及的運動類型,一次查出它們的價格規則、每日尖峰時間,營業時間也只查一次
            List<int> sportTypeIds = new List<int>();
            foreach (UsableVenueRow venue in venues)
            {
                if (!sportTypeIds.Contains(venue.SportTypeId))
                {
                    sportTypeIds.Add(venue.SportTypeId);
                }
            }

            List<CWeekBusinessHourWrap> businessHours = await LoadBusinessHoursAsync(cancellationToken);

            //只取啟用中的價格規則;停用的規則視同沒有價格
            List<SportTypePriceRule> priceRules = await _db.SportTypePriceRules.AsNoTracking()
                .Where(r => sportTypeIds.Contains(r.SportTypeId) && r.IsActive == true)
                .ToListAsync(cancellationToken);

            List<SportTypePeakHour> allPeakHours = await _db.SportTypePeakHours.AsNoTracking()
                .Where(p => sportTypeIds.Contains(p.SportTypeId))
                .ToListAsync(cancellationToken);

            //4. 同一個運動類型的價格摘要都一樣,每個運動類型只算一次
            Dictionary<int, VenuePriceSummary> priceSummaryBySportType = new Dictionary<int, VenuePriceSummary>();
            foreach (int id in sportTypeIds)
            {
                SportTypePriceRule? priceRule = null;
                foreach (SportTypePriceRule rule in priceRules)
                {
                    if (rule.SportTypeId == id)
                    {
                        priceRule = rule;
                    }
                }

                List<CSportTypePeakHourWrap> peakHours = new List<CSportTypePeakHourWrap>();
                foreach (SportTypePeakHour peakHour in allPeakHours)
                {
                    if (peakHour.SportTypeId == id)
                    {
                        CSportTypePeakHourWrap wrap = new CSportTypePeakHourWrap();
                        wrap.sportTypePeakHour = peakHour;
                        peakHours.Add(wrap);
                    }
                }

                priceSummaryBySportType[id] = BuildPriceSummary(priceRule, peakHours, businessHours);
            }

            //5. 組出回傳清單(順序沿用第 1 步的排序)
            foreach (UsableVenueRow venue in venues)
            {
                result.Add(ToUsableVenueInfo(venue, priceSummaryBySportType[venue.SportTypeId]));
            }

            return result;
        }


        public async Task<IReadOnlyList<UsableSportTypeInfo>> GetUsableSportTypesAsync(
            CancellationToken cancellationToken = default)
        {
            //只取存在的運動類型(SportTypes.IsActive 是軟刪除用的系統欄位),用 Select 只取需要的兩個欄位
            return await _db.SportTypes.AsNoTracking()
                .Where(s => s.IsActive == true)
                .OrderBy(s => s.SportTypeId)
                .Select(s => new UsableSportTypeInfo
                {
                    SportTypeId = s.SportTypeId,
                    SportName = s.SportName
                })
                .ToListAsync(cancellationToken);
        }


        /***** 私有方法:查資料 *****/

        //查一個「可以使用」的場地:場地存在(Venues.IsActive)而且它的運動類型也存在(SportTypes.IsActive)
        //任一條件不成立、或查無此場地 >> null
        //GetDayScheduleAsync、GetRangeScheduleAsync、GetUsableVenueAsync 共用,三者對「不能使用」的判斷一致
        private async Task<UsableVenueRow?> LoadUsableVenueAsync(int venueId, CancellationToken cancellationToken)
        {
            return await (from v in _db.Venues.AsNoTracking()
                          join s in _db.SportTypes.AsNoTracking() on v.SportTypeId equals s.SportTypeId
                          where v.VenueId == venueId && v.IsActive == true && s.IsActive == true
                          select new UsableVenueRow
                          {
                              VenueId = v.VenueId,
                              VenueName = v.VenueName,
                              SportTypeId = v.SportTypeId,
                              SportName = s.SportName,
                              Capacity = v.Capacity,
                              PhotoPath = v.PhotoPath
                          })
                         .FirstOrDefaultAsync(cancellationToken);
        }


        //查 7 天營業時間,包成 Wrap(Factory 的規則方法收的是 Wrap)
        private async Task<List<CWeekBusinessHourWrap>> LoadBusinessHoursAsync(CancellationToken cancellationToken)
        {
            List<WeekBusinessHour> rows = await _db.WeekBusinessHours.AsNoTracking()
                .ToListAsync(cancellationToken);

            List<CWeekBusinessHourWrap> list = new List<CWeekBusinessHourWrap>();
            foreach (WeekBusinessHour row in rows)
            {
                CWeekBusinessHourWrap wrap = new CWeekBusinessHourWrap();
                wrap.weekBusinessHour = row;
                list.Add(wrap);
            }

            return list;
        }


        //查某運動類型「啟用中」的價格規則;沒有規則或規則停用(SportTypePriceRules.IsActive = false)都回傳 null
        //停用是業務上的暫停定價,場地照常可以使用,只是沒有價格
        private async Task<SportTypePriceRule?> LoadActivePriceRuleAsync(int sportTypeId, CancellationToken cancellationToken)
        {
            return await _db.SportTypePriceRules.AsNoTracking()
                .Where(r => r.SportTypeId == sportTypeId && r.IsActive == true)
                .FirstOrDefaultAsync(cancellationToken);
        }


        //查某運動類型的每日尖峰時間(正常 7 筆,還沒設定過是 0 筆),包成 Wrap
        private async Task<List<CSportTypePeakHourWrap>> LoadPeakHoursAsync(int sportTypeId, CancellationToken cancellationToken)
        {
            List<SportTypePeakHour> rows = await _db.SportTypePeakHours.AsNoTracking()
                .Where(p => p.SportTypeId == sportTypeId)
                .ToListAsync(cancellationToken);

            List<CSportTypePeakHourWrap> list = new List<CSportTypePeakHourWrap>();
            foreach (SportTypePeakHour row in rows)
            {
                CSportTypePeakHourWrap wrap = new CSportTypePeakHourWrap();
                wrap.sportTypePeakHour = row;
                list.Add(wrap);
            }

            return list;
        }


        /***** 私有方法:組資料(不查DB) *****/

        //組出某一天的每一格時段 >> GetDayScheduleAsync、GetRangeScheduleAsync 共用,兩者的結果一定一致
        //公休日 >> 空清單(ExpandToSlots 對公休日回傳空清單)
        private List<VenueSlotInfo> BuildDaySlots(
            DateOnly date,
            List<CWeekBusinessHourWrap> businessHours,
            HashSet<TimeOnly> unavailableTimes,
            SportTypePriceRule? priceRule,
            List<CSportTypePeakHourWrap> peakHours)
        {
            List<VenueSlotInfo> slots = new List<VenueSlotInfo>();

            //這天是星期幾的營業時間
            CWeekBusinessHourWrap? businessHour = null;
            foreach (CWeekBusinessHourWrap item in businessHours)
            {
                if (item.DayOfWeek == date.DayOfWeek)
                {
                    businessHour = item;
                }
            }

            //這天的尖峰起始時間(沒有資料或那天不分尖峰/離峰都是 null)
            TimeOnly? dayPeakStartTime = null;
            foreach (CSportTypePeakHourWrap peakHour in peakHours)
            {
                if (peakHour.DayOfWeek == date.DayOfWeek)
                {
                    dayPeakStartTime = peakHour.PeakStartTime;
                }
            }

            //營業時間切成一小時一格(已依時間排序),逐格判斷
            foreach (TimeOnly slotTime in _weekBusinessHourFactory.ExpandToSlots(businessHour))
            {
                VenueSlotInfo slot = new VenueSlotInfo();
                slot.SlotTime = slotTime;
                slot.IsUnavailable = unavailableTimes.Contains(slotTime);

                if (priceRule == null)
                {
                    //沒有價格規則或規則停用 >> 沒有尖峰、沒有單價
                    slot.IsPeak = false;
                    slot.UnitPrice = null;
                }
                else
                {
                    slot.IsPeak = _sportTypePriceRuleFactory.IsPeakSlot(slotTime, dayPeakStartTime);

                    if (slot.IsPeak)
                    {
                        slot.UnitPrice = priceRule.PeakPrice;
                    }
                    else
                    {
                        slot.UnitPrice = priceRule.OffPeakPrice;
                    }
                }

                slots.Add(slot);
            }

            return slots;
        }


        //組價格摘要 >> GetUsableVenueAsync、SearchUsableVenuesAsync 共用
        //沒有價格規則或規則停用 >> 價格都是 null、摘要是空字串
        //尖峰價只在「有任何一個營業日有尖峰」時才給(用摘要是否為空字串判斷,跟價格規則列表的判斷方式一致)
        private VenuePriceSummary BuildPriceSummary(
            SportTypePriceRule? priceRule,
            List<CSportTypePeakHourWrap> peakHours,
            List<CWeekBusinessHourWrap> businessHours)
        {
            VenuePriceSummary summary = new VenuePriceSummary();

            if (priceRule == null)
            {
                summary.OffPeakPrice = null;
                summary.PeakPrice = null;
                summary.PeakSummary = string.Empty;
                return summary;
            }

            //摘要文字由 Factory 產生:公休日跳過、不分尖峰/離峰的日子不列出;所有營業日都沒有尖峰時是空字串
            string peakSummary = _sportTypePriceRuleFactory.BuildPeakSummary(peakHours, businessHours);

            summary.OffPeakPrice = priceRule.OffPeakPrice;
            summary.PeakSummary = peakSummary;

            if (peakSummary == string.Empty)
            {
                summary.PeakPrice = null;
            }
            else
            {
                summary.PeakPrice = priceRule.PeakPrice;
            }

            return summary;
        }


        //把內部查詢結果轉成對外的回傳類別
        private UsableVenueInfo ToUsableVenueInfo(UsableVenueRow venue, VenuePriceSummary price)
        {
            UsableVenueInfo info = new UsableVenueInfo();
            info.VenueId = venue.VenueId;
            info.VenueName = venue.VenueName;
            info.SportName = venue.SportName;
            info.Capacity = venue.Capacity;
            info.PhotoPath = venue.PhotoPath;
            info.Price = price;
            return info;
        }


        //內部用的查詢結果:比對外的 UsableVenueInfo 多一個 SportTypeId(查價格規則要用,但不對外提供)
        private sealed class UsableVenueRow
        {
            public int VenueId { get; set; }
            public string VenueName { get; set; } = string.Empty;
            public int SportTypeId { get; set; }
            public string SportName { get; set; } = string.Empty;
            public int? Capacity { get; set; }
            public string? PhotoPath { get; set; }
        }
    }
}
