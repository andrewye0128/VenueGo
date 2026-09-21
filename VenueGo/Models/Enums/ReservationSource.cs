using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    public enum ReservationSource : byte
    {
        /// <summary>
        /// 後台代客建立：由管理員在後台代會員建立預約。
        /// 【期中會使用】期中唯一會用到的來源。
        /// </summary>
        [Display(Name = "後台代客建立")]
        Admin = 0,

        /// <summary>
        /// 會員網頁預約：會員自行於前台網頁完成預約。
        /// 【期末功能】先行定義，期末前台上線即可直接使用。
        /// </summary>
        [Display(Name = "會員網頁預約")]
        Web = 1,
    }
}
