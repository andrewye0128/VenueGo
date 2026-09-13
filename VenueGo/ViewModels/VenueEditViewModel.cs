using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using VenueGo.Models.Entities;
using VenueGo.Models.VenueModels;

namespace VenueGo.ViewModels
{
    public class VenueEditViewModel
    {

        public int VenueId { get; set; }


        [Required]
        [Display(Name = "場地名稱")]
        public string VenueName { get; set; }

        [Required]
        [Display(Name = "地點")]
        public string Location { get; set; }

        [Required]
        [Display(Name = "容納人數")]
        public int? Capacity { get; set; }

        [Display(Name = "照片路徑")]
        public string? PhotoPath { get; set; }

        [Display(Name = "上傳照片")]
        public IFormFile? PhotoFile { get; set; }   // 使用者上傳的檔案本身


        [Display(Name = "運動種類")]
        public int SportTypeId { get; set; }   // 拿掉 [Required]，int 不可能是 null
        //下拉選單的資料
        [ValidateNever] //該欄位不參與驗證
        public IEnumerable<SelectListItem> SportTypes { get; set; }
        
    }

}

