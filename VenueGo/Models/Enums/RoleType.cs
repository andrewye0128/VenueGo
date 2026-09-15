using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    /// <summary>
    /// 角色（Roles.RoleId / UserRoles.RoleId）。
    /// <para>
    /// 對應資料庫 Roles 表已建立的四筆種子資料，數值不可任意變更。
    /// </para>
    /// <para>
    /// 【用途】篩選「一般會員」時使用。新增預約只能選擇具有
    /// <see cref="Member"/> 角色的使用者，否則員工帳號也會出現在會員清單中。
    /// </para>
    /// </summary>
    public enum RoleType
    {
        /// <summary>一般會員。</summary>
        [Display(Name = "一般會員")]
        Member = 1,

        /// <summary>員工。</summary>
        [Display(Name = "員工")]
        Staff = 2,

        /// <summary>場館營運管理者。</summary>
        [Display(Name = "場館營運管理者")]
        Manager = 3,

        /// <summary>系統管理員。</summary>
        [Display(Name = "系統管理員")]
        Admin = 4
    }
}