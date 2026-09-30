namespace VenueGo.ViewModels.VenueViewModels
{
    //價格規則列表整頁 >> 專供 SportTypePriceRuleIndex 頁面使用
    //除了每一筆價格規則,頁面頂端還要顯示一次「場館公休日」提醒(全場館共用,不在每一列重複),
    //所以另外包一層整頁的 ViewModel
    public class SportTypePriceRuleIndexPageViewModel
    {
        public List<SportTypePriceRuleIndexViewModel> Rules { get; set; } = new List<SportTypePriceRuleIndexViewModel>();

        //場館公休日,例如「週一」或「週一、週三」;沒有公休日時是空字串,頁面就不顯示提醒
        public string ClosedDaysText { get; set; } = string.Empty;
    }
}
