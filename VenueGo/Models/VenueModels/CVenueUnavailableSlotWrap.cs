using System.ComponentModel.DataAnnotations;
using VenueGo.Models.Entities;

namespace VenueGo.Models.VenueModels
{
    public class CVenueUnavailableSlotWrap
    {
        //設定wrap class >> 三步驟
        private VenueUnavailableSlot _venueUnavailableSlot;
        public VenueUnavailableSlot venueUnavailableSlot { get { return _venueUnavailableSlot; } set { _venueUnavailableSlot = value; } }
        public CVenueUnavailableSlotWrap() { _venueUnavailableSlot = new VenueUnavailableSlot(); }


        //綁定對應欄位
        [Key]
        public int VenueUnavailableSlotId { get { return _venueUnavailableSlot.VenueUnavailableSlotId; } set { _venueUnavailableSlot.VenueUnavailableSlotId = value; } }

        public int VenueId { get { return _venueUnavailableSlot.VenueId; } set { _venueUnavailableSlot.VenueId = value; } }

        [Display(Name = "不開放日期")]
        public DateOnly UnavailableDate { get { return _venueUnavailableSlot.UnavailableDate; } set { _venueUnavailableSlot.UnavailableDate = value; } }

        //只存起始時間,結束時間=起始+1小時,由程式計算,不存資料庫
        [Display(Name = "不開放時段")]
        public TimeOnly UnavailableTime { get { return _venueUnavailableSlot.UnavailableTime; } set { _venueUnavailableSlot.UnavailableTime = value; } }

        [Display(Name = "不開放原因")]
        [Required(ErrorMessage = "不開放原因不可空白")]
        public string Reason { get { return _venueUnavailableSlot.Reason; } set { _venueUnavailableSlot.Reason = value; } }

        public DateTime CreatedAt { get { return _venueUnavailableSlot.CreatedAt; } set { _venueUnavailableSlot.CreatedAt = value; } }

        public int CreatedBy { get { return _venueUnavailableSlot.CreatedBy; } set { _venueUnavailableSlot.CreatedBy = value; } }
    }
}
