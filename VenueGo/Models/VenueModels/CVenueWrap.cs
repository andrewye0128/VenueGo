using System.ComponentModel.DataAnnotations;
using VenueGo.Models.Entities;

namespace VenueGo.Models.VenueModels
{
    public class CVenueWrap
    {
        //設定wrap class >> 三步驟
        private Venue _venue;
        public Venue venue { get { return _venue; } set { _venue = value; } }
        public CVenueWrap() { _venue = new Venue(); }


        //綁定對應欄位
        [Key]
        public int VenueId { get { return _venue.VenueId; } set { _venue.VenueId = value; } }

        [Display(Name = "場地名稱")]
        [Required(ErrorMessage = "場地名稱不可空白")]
        public string VenueName { get { return _venue.VenueName; } set { _venue.VenueName = value; } }
        [Display(Name = "運動類型")]
        public int SportTypeId { get { return _venue.SportTypeId; } set { _venue.SportTypeId = value; } }
        [Display(Name = "位置")]
        [Required(ErrorMessage = "位置描述不可空白")]
        public string Location { get { return _venue.Location; } set { _venue.Location = value; } }

        public bool IsActive { get { return _venue.IsActive; } set { _venue.IsActive = value; } }
        [Display(Name = "容納人數")]
        public int? Capacity { get { return _venue.Capacity; } set { _venue.Capacity = value; } }
        [Display(Name = "場地照片")]
        public string? PhotoPath { get { return _venue.PhotoPath; } set { _venue.PhotoPath = value; } }

        public DateTime CreatedAt { get { return _venue.CreatedAt; } set { _venue.CreatedAt = value; } }

        public int CreatedBy { get { return _venue.CreatedBy; } set { _venue.CreatedBy = value; } }

        public DateTime? UpdatedAt { get { return _venue.UpdatedAt; } set { _venue.UpdatedAt = value; } }

        public int? UpdatedBy { get { return _venue.UpdatedBy; } set { _venue.UpdatedBy = value; } }
    }
}
