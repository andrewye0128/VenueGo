using System.ComponentModel.DataAnnotations;
using VenueGo.Models.Entities;

namespace VenueGo.Models.VenueModels
{
    public class CSportTypeWrap
    {
        //設定wrap class >> 三步驟
        private SportType _sportType;
        public SportType sportType { get { return _sportType; } set { _sportType = value; } }
        public CSportTypeWrap() { _sportType = new SportType(); }

        //綁定對應欄位
        [Key]
        public int SportTypeId { get { return _sportType.SportTypeId; } set { _sportType.SportTypeId = value; } }

        [Required(ErrorMessage = "運動種類名稱不可空白")]
        [Display(Name = "運動類型名稱")]
        public string SportName { get { return _sportType.SportName; } set { _sportType.SportName = value; } }


        public bool IsActive { get { return _sportType.IsActive; } set { _sportType.IsActive = value; } }


        public DateTime CreatedAt { get { return _sportType.CreatedAt; } set { _sportType.CreatedAt = value; } }


        public int CreatedBy { get { return _sportType.CreatedBy; } set { _sportType.CreatedBy = value; } }


        public DateTime? UpdatedAt { get { return _sportType.UpdatedAt; } set { _sportType.UpdatedAt = value; } }


        public int? UpdatedBy { get { return _sportType.UpdatedBy; } set { _sportType.UpdatedBy = value; } }
    }
}
