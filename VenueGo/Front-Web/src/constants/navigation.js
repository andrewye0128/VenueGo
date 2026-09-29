// 全站導覽連結：Header（NavBar / NavDrawer）與 Footer 共用
// 改網址只要改這裡，Header 和 Footer 會一起更新
// 目前只有 /booking 有頁面，其他路由由負責的組員補上

const booking = { label: "場地預約", to: "/booking" };
const pricing = { label: "收費標準", to: "/pricing" };
const venues = { label: "場館資訊", to: "/venues" };
const transport = { label: "交通方式", to: "/venues/transport" };
const news = { label: "最新消息", to: "/news" };
const reviews = { label: "會員評價", to: "/reviews" };
const faq = { label: "常見問題", to: "/faq" };

// Header 主選單
export const mainNavItems = [booking, pricing, venues, news, reviews, faq];

// Footer 連結分組
export const footerLinkGroups = [
  {
    title: "預約服務",
    links: [booking, pricing],
  },
  {
    title: "會員",
    links: [
      { label: "個人資料", to: "/member/profile" },
      { label: "我的預約", to: "/member/reservations" },
      { label: "訂單付款", to: "/member/orders" },
      { label: "我的票券", to: "/member/tickets" },
      { label: "我的評價", to: "/member/reviews" },
    ],
  },
  {
    title: "場館",
    links: [venues, transport, news, reviews, faq],
  },
];
