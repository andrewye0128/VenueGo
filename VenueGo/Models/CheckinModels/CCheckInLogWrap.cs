using System.ComponentModel.DataAnnotations;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;

namespace VenueGo.Models.CheckinModels
{
    public class CCheckInLogWrap
    {
        private CheckInLog _checkInLog;
        public CheckInLog checkInLog { get { return _checkInLog; } set { _checkInLog = value; } }
        public CCheckInLogWrap() { _checkInLog = new CheckInLog(); }

        [Key]
        public int LogId
        {
            get { return _checkInLog.LogId; }
            set { _checkInLog.LogId = value; }
        }

        public int TicketId
        {
            get { return _checkInLog.TicketId; }
            set { _checkInLog.TicketId = value; }
        }

        [Display(Name = "動作")]
        public byte Action
        {
            get { return _checkInLog.Action; }
            set { _checkInLog.Action = value; }
        }

        [Display(Name = "時間")]
        public DateTime ActionTime
        {
            get { return _checkInLog.ActionTime; }
            set { _checkInLog.ActionTime = value; }
        }

        public bool IsValid
        {
            get { return _checkInLog.IsValid; }
            set { _checkInLog.IsValid = value; }
        }

        [Display(Name = "人工覆核")]
        public bool IsManualOverride
        {
            get { return _checkInLog.IsManualOverride; }
            set { _checkInLog.IsManualOverride = value; }
        }

        [Display(Name = "員工編號")]
        public int? OperatorId
        {
            get { return _checkInLog.OperatorId; }
            set { _checkInLog.OperatorId = value; }
        }

        public byte? FailReason
        {
            get { return _checkInLog.FailReason; }
            set { _checkInLog.FailReason = value; }
        }
    }
}
