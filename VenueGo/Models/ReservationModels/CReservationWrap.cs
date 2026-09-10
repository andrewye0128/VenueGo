using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using VenueGo.Models.Entities;

namespace VenueGo.Models.ReservationModels
{
    public class CReservationWrap
    {
        private Reservation _reservation;

        public Reservation reservation { get { return _reservation; } set { _reservation = value; } }

        public CReservationWrap() { _reservation = new Reservation(); }

        [DisplayName("預約編號")]
        [Key]
        public int ReservationId {
            get { return _reservation.ReservationId; }
            set { _reservation.ReservationId = value; }
        }

        [DisplayName("使用者編號")]
        public int UserId { 
            get { return _reservation.UserId; }
            set { _reservation.UserId = value; }
        }

        [DisplayName("場地編號")]
        public int VenueId { 
            get { return _reservation.VenueId; }
            set { _reservation.VenueId = value; }
        }

        [DisplayName("預約日期")]
        public DateOnly BookingDate { 
            get { return _reservation.BookingDate; }
            set { _reservation.BookingDate = value; }
        }

        [DisplayName("開始時間")]
        public TimeOnly StartTime { 
            get { return _reservation.StartTime; }
            set { _reservation.StartTime = value; }
        }

        [DisplayName("結束時間")]
        public TimeOnly EndTime { 
            get { return _reservation.EndTime; }
            set { _reservation.EndTime = value; }
        }

        [DisplayName("預約建立時間")]
        public DateTime ReservedAt { 
            get { return _reservation.ReservedAt; }
            set { _reservation.ReservedAt = value; }
        }

        [DisplayName("預約狀態")]
        public byte ReservationStatus { 
            get { return _reservation.ReservationStatus; }
            set { _reservation.ReservationStatus = value; }
        }

        [DisplayName("付款截止時間")]
        public DateTime PaymentDueAt { 
            get { return _reservation.PaymentDueAt; }
            set { _reservation.PaymentDueAt = value; }
        }

        [DisplayName("同意預約須知時間")]
        public DateTime TermsAcceptedAt { 
            get { return _reservation.TermsAcceptedAt; }
            set { _reservation.TermsAcceptedAt = value; }
        }

        [DisplayName("預約須知版本")]
        public string TermsVersion { 
            get { return _reservation.TermsVersion; }
            set { _reservation.TermsVersion = value; }
        }
    }
}
