using System.ComponentModel.DataAnnotations;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;

namespace VenueGo.Models.CheckinModels
{
    public class CTicketStatusLogWrap
    {
        private TicketStatusLog _ticketStatusLog;
        public TicketStatusLog ticketStatusLog { get { return _ticketStatusLog; } set { _ticketStatusLog = value; } }
        public CTicketStatusLogWrap() { _ticketStatusLog = new TicketStatusLog(); }

        [Key]
        public int LogId
        {
            get { return _ticketStatusLog.LogId; }
            set { _ticketStatusLog.LogId = value; }
        }

        public int TicketId
        {
            get { return _ticketStatusLog.TicketId; }
            set { _ticketStatusLog.TicketId = value; }
        }

        public byte ActionType
        {
            get { return _ticketStatusLog.ActionType; }
            set { _ticketStatusLog.ActionType = value; }
        }

        public byte FromStatus
        {
            get { return _ticketStatusLog.FromStatus; }
            set { _ticketStatusLog.FromStatus = value; }
        }

        public byte ToStatus
        {
            get { return _ticketStatusLog.ToStatus; }
            set { _ticketStatusLog.ToStatus = value; }
        }

        public byte? ReasonType
        {
            get { return _ticketStatusLog.ReasonType; }
            set { _ticketStatusLog.ReasonType = value; }
        }

        [Display(Name = "備註")]
        public string? Reason
        {
            get { return _ticketStatusLog.Reason; }
            set { _ticketStatusLog.Reason = value; }
        }

        [Display(Name = "員工編號")]
        public int? OperatorId
        {
            get { return _ticketStatusLog.OperatorId; }
            set { _ticketStatusLog.OperatorId = value; }
        }

        [Display(Name = "時間")]
        public DateTime CreatedAt
        {
            get { return _ticketStatusLog.CreatedAt; }
            set { _ticketStatusLog.CreatedAt = value; }
        }
    }
}
