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


        //批次修改 >> 一次把7天的營業時間設定存回去,7筆都在同一個DbContext裡處理,最後一次SaveChanges
        public void EditAll(List<CWeekBusinessHourWrap> wraps)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                foreach (var wrap in wraps)
                {
                    //依BusinessHoursId查找對應資料
                    var data = db.WeekBusinessHours.FirstOrDefault(w => w.BusinessHoursId == wrap.BusinessHoursId);
                    if (data != null)
                    {
                        //DayOfWeek不開放編輯,故意不覆蓋,只更新以下欄位
                        data.IsOpen = wrap.IsOpen;
                        data.OpenTime = wrap.OpenTime;
                        data.CloseTime = wrap.CloseTime;
                    }
                }

                db.SaveChanges();
            }
        }
    }
}