namespace VenueGo.Constants
{
    /// <summary>
    /// 前台（Vue，Front-Web）的頁面路徑：後端要把人導到前台時用。
    /// 要跟 Front-Web/src/router/index.js 一致，前台改路徑時這裡要一起改
    /// </summary>
    public static class FrontendRoutes
    {
        /// <summary>會員中心「訂單與付款」</summary>
        public const string MemberOrders = "/member/orders";
    }
}