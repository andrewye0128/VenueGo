// 全站導覽連結：Header（NavBar / NavDrawer）與 Footer 共用
// 改網址只要改這裡，Header 和 Footer 會一起更新
// 目前只有 /booking 有頁面，其他路由由負責的組員補上

const about = { label: "關於我們", to: "/about" };
const booking = { label: "場地預約", to: "/booking" };
const pricing = { label: "收費標準", to: "/pricing" };
const venues = { label: "場館資訊", to: "/venues" };
const transport = { label: "交通方式", to: "/venues/transport" };
const news = { label: "最新消息", to: "/news" };
const reviews = { label: "顧客評價", to: "/reviews" };
const faq = { label: "常見問題", to: "/faq" };

// Header 主選單
export const mainNavItems = [about, booking, venues, news, reviews, faq];

// 會員中心的頁面：會員中心左側選單（MemberLayout）、Header 頭像的下拉選單（MemberMenuPanel）、
// Footer「會員」分組共用。新增頁面、改名稱只要改這裡，三個地方會一起更新
// 圖示名稱要寫完整，Nuxt UI 打包時才掃描得到（Footer 沒有顯示圖示）
export const memberNavItems = [
  { label: "個人資料", to: "/member/profile", icon: "i-mdi-card-account-details-outline" },
  { label: "我的預約", to: "/member/reservations", icon: "i-mdi-calendar-month-outline" },
  { label: "訂單與付款", to: "/member/orders", icon: "i-mdi-credit-card-outline" },
  { label: "我的票券", to: "/member/tickets", icon: "i-mdi-ticket-confirmation-outline" },
  { label: "我的評論", to: "/member/reviews", icon: "i-mdi-star-outline" },
];

// Footer 連結分組
export const footerLinkGroups = [
  {
    title: "預約服務",
    links: [booking],
  },
  {
    // 11/03：改用 memberNavItems（原本自己寫一份，「訂單與付款」寫成「訂單付款」，跟會員中心不一致）
    title: "會員",
    links: memberNavItems,
  },
  {
    title: "場館",
    links: [venues, transport, news, reviews, faq],
  },
];
