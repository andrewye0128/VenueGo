using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;

namespace VenueGo.Models.CheckinModels
{
    public class CTicketStatusLogFactory
    {
        private readonly dbVenueContext _db;

        public CTicketStatusLogFactory(dbVenueContext db)
        {
            _db = db;
        }

        //異動紀錄查詢 >> 給票券詳情「異動紀錄」分頁顯示用,新的在最上面
        public List<CTicketStatusLogWrap> QueryByTicket(int ticketId)
        {
            List<CTicketStatusLogWrap> list = new List<CTicketStatusLogWrap>();

            var datas = _db.TicketStatusLogs.AsNoTracking()
                .Where(l => l.TicketId == ticketId)
                .OrderByDescending(l => l.LogId)
                .ToList();

            foreach (TicketStatusLog data in datas)
            {
                CTicketStatusLogWrap wrap = new CTicketStatusLogWrap();
                wrap.ticketStatusLog = data;
                list.Add(wrap);
            }

            return list;
        }

        //下拉選單 >> 這個動作可以選的原因類別(顯示文字取自 enum 的 Display 名稱)
        public List<SelectListItem> GetReasonOptions(TicketManualLogAction action)
        {
            List<SelectListItem> list = new List<SelectListItem>();

            foreach (TicketManualLogReasonType reason in GetAllowedReasons(action))
            {
                list.Add(new SelectListItem
                {
                    Text = reason.GetDisplayName(),
                    Value = ((byte)reason).ToString()
                });
            }

            return list;
        }

        //防呆用 >> 檢查人工異動表單有沒有填對,沒問題回傳 null,有問題回傳要顯示的錯誤訊息
        public string? ValidateInput(TicketManualLogAction action, TicketManualLogReasonType? reasonType, string? remark)
        {
            if (reasonType == null)
                return "請選擇原因類別";

            //原因類別一定要是這個動作允許的,避免有人用工具硬送不合理的組合(例如取消 + QR 遺失)
            if (!GetAllowedReasons(action).Contains(reasonType.Value))
                return "原因類別不適用於這個操作";

            if (reasonType == TicketManualLogReasonType.Other && string.IsNullOrWhiteSpace(remark))
                return "選擇「其他」時必須填寫備註";

            //對應資料庫 Reason 欄位 NVARCHAR(200)
            if (remark != null && remark.Trim().Length > 200)
                return "備註最多 200 字";

            return null;
        }

        //共用私有方法 >> 每個動作可以選哪些原因,下拉選單和防呆驗證都呼叫這裡,規則只寫一份
        private List<TicketManualLogReasonType> GetAllowedReasons(TicketManualLogAction action)
        {
            List<TicketManualLogReasonType> reasons = new List<TicketManualLogReasonType>();

            if (action == TicketManualLogAction.Cancel)
            {
                //取消(作廢 = 取消 + 原因選「重複或誤購」或「疑似異常」)
                reasons.Add(TicketManualLogReasonType.CustomerRequest);
                reasons.Add(TicketManualLogReasonType.VenueIssue);
                reasons.Add(TicketManualLogReasonType.DuplicateOrMistake);
                reasons.Add(TicketManualLogReasonType.SuspectedAbuse);
                reasons.Add(TicketManualLogReasonType.SystemError);
                reasons.Add(TicketManualLogReasonType.Other);
            }
            else if (action == TicketManualLogAction.Expire)
            {
                reasons.Add(TicketManualLogReasonType.CustomerRequest);
                reasons.Add(TicketManualLogReasonType.SuspectedAbuse);
                reasons.Add(TicketManualLogReasonType.SystemError);
                reasons.Add(TicketManualLogReasonType.Other);
            }
            else if (action == TicketManualLogAction.Reissue)
            {
                //補發 QR
                reasons.Add(TicketManualLogReasonType.QrLostOrLeaked);
                reasons.Add(TicketManualLogReasonType.SystemError);
                reasons.Add(TicketManualLogReasonType.Other);
            }

            return reasons;
        }
    }
}
