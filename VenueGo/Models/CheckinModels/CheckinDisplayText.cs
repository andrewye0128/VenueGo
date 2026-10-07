using VenueGo.Models.Enums;

namespace VenueGo.Models.CheckinModels
{
    //報到與票券管理的「顯示用文字」集中在這裡
    public static class CheckinDisplayText
    {
        // ---------- 掃描紀錄(CheckInLog) ----------

        //動作(入場/離場)
        public static string ActionText(byte action)
        {
            if (action == (byte)CheckInAction.CheckIn) return "入場";
            if (action == (byte)CheckInAction.CheckOut) return "離場";
            return $"未知({action})";
        }

        
        public static string FailReasonText(bool isValid, byte? failReason)
        {
            if (isValid) return "";
            // 防止有先前的紀錄現在設定一定要有原因
            if (failReason == null) return "（未記錄）";
            return ((CheckInFailReason)failReason.Value).GetDisplayName();
        }

        //操作結果 >> 成功顯示「入場成功」,失敗顯示失敗原因
        public static string ResultText(byte action, bool isValid, byte? failReason)
        {
            if (isValid) return ActionText(action) + "成功";
            return FailReasonText(isValid, failReason);
        }

        // ---------- 異動紀錄(TicketStatusLog) ----------

        //動作(取消/轉失效/補發...)
        public static string ManualActionText(byte actionType)
        {
            return ((TicketManualLogAction)actionType).GetDisplayName();
        }

        //狀態變化,例如「有效 → 已取消」
        public static string StatusChangeText(byte fromStatus, byte toStatus)
        {
            string from = ((EntryTicketStatus)fromStatus).GetDisplayName();
            string to = ((EntryTicketStatus)toStatus).GetDisplayName();
            return $"{from} → {to}";
        }

        //原因類別,舊資料沒有原因類別
        public static string ReasonTypeText(byte? reasonType)
        {
            if (reasonType == null) return "（歷史資料）";
            return ((TicketManualLogReasonType)reasonType.Value).GetDisplayName();
        }

        //備註,沒填顯示破折號
        public static string ReasonText(string? reason)
        {
            if (string.IsNullOrEmpty(reason)) return "—";
            return reason;
        }
    }
}
