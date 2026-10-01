using System.Reflection.Metadata.Ecma335;
using VenueGo.Data;
using VenueGo.Dtos.VenueDtos;
using VenueGo.Models.Entities;
using VenueGo.ViewModels.VenueViewModels;

namespace VenueGo.Models.VenueModels
{
    public class CSportTypeFactory
    {
        /*對SportType表做CRUD方法集中處*/

        //撈取所有資料 >> QueryAll
        public List<CSportTypeWrap> QueryAll()
        {
            //準備要回傳的變數 >> list
            List<CSportTypeWrap> list = new List<CSportTypeWrap>();
            //撈取資料 >> 撈出集合形式
            dbVenueContext db = new dbVenueContext();
            var datas = from t in db.SportTypes
                        where t.IsActive == true
                        select t;

            //對IQueryable集合元素逐項放入list
            foreach (var item in datas)
            {
                CSportTypeWrap x = new CSportTypeWrap();
                x.sportType = item;
                list.Add(x);
            }

            return list;
        }


        //依照id撈取對應資料 >> QueryById
        public CSportTypeWrap QueryById(int? id)
        {
            CSportTypeWrap SportTypeWrap = new CSportTypeWrap();
            SportType? SportTypeDb = null;
            dbVenueContext db = new dbVenueContext();
            if (id != null)
            {
                SportTypeDb = db.SportTypes.FirstOrDefault(t => t.SportTypeId == (int)id);
            }

            if (SportTypeDb == null)
            {
                return new CSportTypeWrap();
            }

            SportTypeWrap.sportType = SportTypeDb;
            return SportTypeWrap;
        }


        //Create
        public void Create(CSportTypeWrap Wrap)
        {
            dbVenueContext db = new dbVenueContext();
            db.SportTypes.Add(Wrap.sportType);
            db.SaveChanges();
        }

        //Delete(軟刪除)
        //userId / now 由 Controller 傳入(登入者 UserId、ITimeService 校時後的時間)
        public void Delete(int id, int userId, DateTime now)
        {
            dbVenueContext db = new dbVenueContext();
            //依照取得的id去尋找對應的SportType
            var data = db.SportTypes.FirstOrDefault(item => item.SportTypeId == id);
            if (data != null)
            {
                data.IsActive = false;

                //軟刪除也是一次更新 >> 記錄是誰、何時停用
                data.UpdatedAt = now;
                data.UpdatedBy = userId;
            }
            db.SaveChanges();
        }

        //Edit
        //userId / now 由 Controller 傳入(登入者 UserId、ITimeService 校時後的時間)
        public void Edit(CSportTypeWrap Wrap, string? newPhotoPath, int userId, DateTime now)
        {
            dbVenueContext db = new dbVenueContext();
            //驗證傳入Wrap非null
            if (Wrap == null)
                return;

            //用傳入Wrap內部的id值查找對應資料
            var SportTypeDb = db.SportTypes.FirstOrDefault(t => t.SportTypeId == Wrap.SportTypeId);
            if (SportTypeDb != null)
            {
                SportTypeDb.SportName = Wrap.SportName;

                //注意事項:後台清空就存 null,保留使用者輸入的換行
                SportTypeDb.Notice = Wrap.Notice;

                //代表照片:有上傳新照片才換,沒上傳就保留資料庫原本的路徑
                //不用 Wrap.PhotoPath >> Wrap 直接接表單,這個值可能被竄改
                if (newPhotoPath != null)
                {
                    SportTypeDb.PhotoPath = newPhotoPath;
                }

                //稽核欄位:記錄最後修改時間與修改者
                SportTypeDb.UpdatedAt = now;
                SportTypeDb.UpdatedBy = userId;
            }

            db.SaveChanges();
        }


        //前台「場館資訊」頁 >> 開放時間 + 各運動類型的代表照片、注意事項、收費標準
        //給 VenueApiController(GET /api/venues/intro)使用,只讀取資料,不修改
        //
        //邏輯步驟:
        //  步驟1 開放時間:查營業時間,排成週一~週日 7 筆(查不到的那天當成公休)
        //  步驟2 運動類型:只列出存在的運動類型(IsActive = true),而且底下至少有一個存在的場地
        //  步驟3 價格規則:重用 CSportTypePriceRuleFactory.QueryAll(),尖峰摘要等規則不重寫
        //  步驟4 組合:每個運動類型配上自己的價格規則;沒有價格規則或價格規則停用 >> Price = null
        //
        //整個方法共查 5 次DB(營業時間 1 次、運動類型 1 次、價格規則 QueryAll() 內部 3 次),都不在迴圈裡查
        public SportTypeIntroPageDto QueryIntroPage()
        {
            SportTypeIntroPageDto page = new SportTypeIntroPageDto();
            CSportTypePriceRuleFactory priceRuleFactory = new CSportTypePriceRuleFactory();


            /***** 步驟1 開放時間 *****/

            //資料庫依 DayOfWeek 排序(星期日 = 0 排最前面),畫面要星期一排最前面、星期日排最後面
            List<CWeekBusinessHourWrap> businessHours = new CWeekBusinessHourFactory().QueryAll();
            DayOfWeek[] displayOrder = new DayOfWeek[]
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
            };

            foreach (DayOfWeek day in displayOrder)
            {
                BusinessHourDayDto dayDto = new BusinessHourDayDto();
                dayDto.DayName = priceRuleFactory.GetShortDayName(day);

                //找出這一天的營業時間;資料表查不到這一天 >> businessHour 維持 null,下面當成公休
                CWeekBusinessHourWrap? businessHour = null;
                foreach (CWeekBusinessHourWrap item in businessHours)
                {
                    if (item.DayOfWeek == day)
                    {
                        businessHour = item;
                        break;
                    }
                }

                //營業的判斷:IsOpen 為 true,而且開始、打烊時間都有值(跟價格規則的 NormalizePeakDays、BuildPeakRows 一致)
                if (businessHour != null && businessHour.IsOpen && businessHour.OpenTime.HasValue && businessHour.CloseTime.HasValue)
                {
                    dayDto.IsOpen = true;
                    //轉成 "HH:mm" 字串:TimeOnly 直接轉 JSON 會變成 "10:00:00"
                    dayDto.OpenTime = businessHour.OpenTime.Value.ToString("HH:mm");
                    dayDto.CloseTime = businessHour.CloseTime.Value.ToString("HH:mm");
                }
                else
                {
                    //公休:時間一律是 null
                    dayDto.IsOpen = false;
                    dayDto.OpenTime = null;
                    dayDto.CloseTime = null;
                }

                page.BusinessHours.Add(dayDto);
            }


            /***** 步驟2 運動類型 *****/

            //只列出存在的運動類型,而且底下至少有一個存在的場地(沒有場地的運動類型不顯示,2026-10-01 HungYu 決定)
            //Venues.IsActive、SportTypes.IsActive 都是軟刪除用的系統欄位:false = 已刪除,當作不存在
            //Any() 在資料庫裡直接判斷有沒有場地,不用把場地全部撈出來
            List<SportType> sportTypes;
            using (dbVenueContext db = new dbVenueContext())
            {
                var datas = from t in db.SportTypes
                            where t.IsActive == true
                               && db.Venues.Any(v => v.SportTypeId == t.SportTypeId && v.IsActive == true)
                            orderby t.SportTypeId
                            select t;

                //在 using 裡面先取出結果,離開 using 之後 DbContext 就關閉了
                sportTypes = datas.ToList();
            }


            /***** 步驟3 價格規則 *****/

            //重用後台價格規則列表的查詢:已經算好 IsActive、離峰/尖峰價、HasAnyPeak、尖峰摘要 PeakSummary
            List<SportTypePriceRuleIndexViewModel> priceRules = priceRuleFactory.QueryAll();


            /***** 步驟4 組合 *****/

            foreach (SportType sportType in sportTypes)
            {
                SportTypeIntroDto introDto = new SportTypeIntroDto();
                introDto.SportTypeId = sportType.SportTypeId;
                introDto.SportName = sportType.SportName;
                introDto.PhotoPath = sportType.PhotoPath;
                introDto.Notice = sportType.Notice;

                //找出這個運動類型的價格規則(一個運動類型最多一筆,唯一索引保證)
                SportTypePriceRuleIndexViewModel? priceRule = null;
                foreach (SportTypePriceRuleIndexViewModel rule in priceRules)
                {
                    if (rule.SportTypeId == sportType.SportTypeId)
                    {
                        priceRule = rule;
                        break;
                    }
                }

                //沒有價格規則,或價格規則停用(暫停定價) >> Price = null,前台顯示「請洽櫃台詢問」
                if (priceRule == null || priceRule.IsActive == false)
                {
                    introDto.Price = null;
                }
                else
                {
                    SportTypePriceDto priceDto = new SportTypePriceDto();
                    priceDto.OffPeakPrice = priceRule.OffPeakPrice;

                    //有任何一個營業日有尖峰,才提供尖峰價與尖峰摘要
                    //不分尖離峰時,資料庫的尖峰價存 0,直接傳出去前台會顯示「尖峰 0 元」,所以改成 null
                    //還沒設定每日尖峰時間的舊價格規則,HasAnyPeak 也是 false >> 當作不分尖離峰,不讀舊欄位(跟 IVenueScheduleService 一致)
                    if (priceRule.HasAnyPeak)
                    {
                        priceDto.PeakPrice = priceRule.PeakPrice;
                        priceDto.PeakSummary = priceRule.PeakSummary;
                    }
                    else
                    {
                        priceDto.PeakPrice = null;
                        priceDto.PeakSummary = string.Empty;
                    }

                    introDto.Price = priceDto;
                }

                page.SportTypes.Add(introDto);
            }

            return page;
        }
    }
}