using VenueGo.Enums;

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


        // ---------- 超時費用(OvertimeCharge) ----------

        //收費狀態:待收/已收/已免收
        public static string OvertimeStatusText(byte status)
        {
            return ((OvertimeChargeStatus)status).GetDisplayName();
        }

        //付款方式,免收或還沒收是 NULL
        public static string OvertimePaymentMethodText(byte? method)
        {
            if (method == null) return "—";
            return ((PaymentMethodOnSite)method.Value).GetDisplayName();
        }

        public static string FailMessage(CheckInFailReason reason, string actionText)
        {
            switch (reason)
            {
                case CheckInFailReason.TicketNotFound:
                    return "找不到這張票券";
                case CheckInFailReason.AlreadyCancelled:
                    return $"票券已取消，無法{actionText}";
                case CheckInFailReason.AlreadyExpired:
                    return $"票券已失效，無法{actionText}";
                case CheckInFailReason.AlreadyCompleted:
                    return $"票券已使用完畢，無法{actionText}";
                case CheckInFailReason.NotYetStartTime:
                    return $"尚未到預約時間，無法{actionText}";
                case CheckInFailReason.InvalidSequence:
                    return InvalidSequenceText(actionText);
                default:
                    return $"{actionText}失敗";
            }
        }

        // 順序異常的說明，依操作不同
        private static string InvalidSequenceText(string actionText)
        {
            if (actionText == "入場") return "入場順序異常（可能已經入場）";
            if (actionText == "離場") return "離場順序異常（這張票還沒入場）";
            if (actionText == "取消") return "只有「有效」狀態的票券才能取消（已使用過的不行）";
            if (actionText == "轉失效") return "只有「有效」狀態的票券才能轉失效";
            return $"{actionText}失敗：異常操作行為";
        }
    }
}
