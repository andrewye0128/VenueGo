using VenueGo.Models.VenueModels;

namespace VenueGo.ViewModels.VenueViewModels
{
    //場地依運動類型分組後的顯示用項目 >> 專供 VenueIndex 頁面使用,純顯示用,不綁定 Entity
    public class VenueGroupViewModel
    {
        public int SportTypeId { get; set; }
        public string SportTypeName { get; set; } = string.Empty;

        //這一組底下所有場地,分頁時整組一起顯示,不會被拆到別頁
        public List<CVenueWrap> Venues { get; set; } = new List<CVenueWrap>();
    }
}
