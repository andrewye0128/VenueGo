using Microsoft.AspNetCore.Mvc.Rendering;
using VenueGo.Data;
using VenueGo.Models.Entities;

namespace VenueGo.Models.VenueModels
{
    public class CWeekBusinessHourFactory
    {
        //開始/結束營業時間下拉選單選項(整點,00:00~23:00)
        //Index頁面OpenTime跟CloseTime共用同一份選項
        //改用下拉選單不用input
        //選項只放整點,UI上不會選到非整點值

        public List<SelectListItem> GetWholeHourOptions()
        {
            List<SelectListItem> list = new List<SelectListItem>();

            //用int(0~23)控制迴圈,不能直接用TimeOnly本身遞增比較:
            //TimeOnly沒有24:00這個值,23:00.AddHours(1)會繞回00:00(跨過午夜),
            //如果迴圈條件寫成time <= 23:00,遇到繞回的00:00還是會小於23:00,造成無窮迴圈
            for (int hour = 0; hour <= 23; hour++)
            {
                TimeOnly time = new TimeOnly(hour, 0);
                string text = time.ToString("HH:mm");
                list.Add(new SelectListItem { Text = text, Value = text });
            }

            return list;
        }


        //回傳全部7天的營業時間設定 >> List,依DayOfWeek排序(星期日排最前面,對應System.DayOfWeek的0)
        public List<CWeekBusinessHourWrap> QueryAll()
        {
            List<CWeekBusinessHourWrap> list = new List<CWeekBusinessHourWrap>();

            using (dbVenueContext db = new dbVenueContext())
            {
                var datas = from t in db.WeekBusinessHours
                            orderby t.DayOfWeek
                            select t;

                //將撈取資料放回 list
                foreach (var data in datas)
                {
                    CWeekBusinessHourWrap wrap = new CWeekBusinessHourWrap();
                    wrap.weekBusinessHour = data;
                    list.Add(wrap);
                }
            }

            return list;
        }


        //依星期幾查詢單筆營業時間設定 >> 給VenueUnavailableSlot功能查詢該天的實際營業時間範圍用
        //理論上7天資料都已經存在,查不到才回傳null(異常情況,呼叫方自行判斷)
        public CWeekBusinessHourWrap GetByDayOfWeek(DayOfWeek day)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                byte dayValue = (byte)day;

                var data = db.WeekBusinessHours.FirstOrDefault(w => w.DayOfWeek == dayValue);

                if (data == null)
                {
                    return null;
                }

                CWeekBusinessHourWrap wrap = new CWeekBusinessHourWrap();
                wrap.weekBusinessHour = data;
                return wrap;
            }
        }


        //把某一天的營業時間切成一小時一格的時段清單(每格記錄開始時間) >> 不查DB,營業時間由呼叫方傳入
        //例:營業 10:00~22:00 >> 10:00、11:00 ... 21:00 共 12 格(最後一格是 21:00~22:00,不會多出 22:00 那一格)
        //公休日、查不到營業時間(businessHour 是 null)、或開始/打烊時間不完整 >> 回傳空清單
        //目前給場地時段服務(VenueScheduleService)使用,是「營業時段怎麼切」這條規則唯一的一份
        public List<TimeOnly> ExpandToSlots(CWeekBusinessHourWrap? businessHour)
        {
            List<TimeOnly> slots = new List<TimeOnly>();

            if (businessHour == null || !businessHour.IsOpen || !businessHour.OpenTime.HasValue || !businessHour.CloseTime.HasValue)
            {
                return slots;
            }

            int openHour = businessHour.OpenTime.Value.Hour;
            int closeHour = businessHour.CloseTime.Value.Hour;

            //條件是 hour < closeHour(不是 <=):每一格代表「從這個時間開始的一小時」,打烊那個整點不能再開始一格
            //用int控制迴圈,理由跟GetWholeHourOptions()一樣:TimeOnly過了23:00會繞回00:00,不能直接拿TimeOnly本身遞增比較
            for (int hour = openHour; hour < closeHour; hour++)
            {
                slots.Add(new TimeOnly(hour, 0));
            }

            return slots;
        }


        //批次修改 >> 一次把7天的營業時間設定存回去,7筆都在同一個DbContext裡處理,最後一次SaveChanges
        //userId / now 由 Controller 傳入(登入者 UserId、ITimeService 校時後的時間)
        public void EditAll(List<CWeekBusinessHourWrap> wraps, int userId, DateTime now)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                foreach (var wrap in wraps)
                {
                    //依BusinessHoursId查找對應資料
                    var data = db.WeekBusinessHours.FirstOrDefault(w => w.BusinessHoursId == wrap.BusinessHoursId);
                    if (data != null)
                    {
                        //表單每次都會送回7天,只有內容真的有變的那天才更新
                        //這樣每一天的UpdatedAt/UpdatedBy才看得出「這一天」最後是誰、何時改的
                        bool isChanged = data.IsOpen != wrap.IsOpen
                                      || data.OpenTime != wrap.OpenTime
                                      || data.CloseTime != wrap.CloseTime;

                        if (isChanged)
                        {
                            //DayOfWeek不開放編輯,故意不覆蓋,只更新以下欄位
                            data.IsOpen = wrap.IsOpen;
                            data.OpenTime = wrap.OpenTime;
                            data.CloseTime = wrap.CloseTime;

                            //稽核欄位:記錄最後修改時間與修改者
                            data.UpdatedAt = now;
                            data.UpdatedBy = userId;
                        }
                    }
                }

                db.SaveChanges();
            }
        }
    }
}