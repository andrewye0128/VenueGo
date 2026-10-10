// ============================================================
//  reviewRoutes.js — 顧客端評論的路由表
//
//  併進組裡的路由（src/router/index.js）：
//      import reviewRoutes from "./reviewRoutes";
//      routes: [ ...原本的路由, ...reviewRoutes, 404 那一筆（一定要在最後） ]
//  導覽列「會員評價」（constants/navigation.js 的 /reviews）就是這裡的評論專區。
//
//  路由（Razor 版 CReview 已在 10/9 退役）：
//    評論專區                → /reviews
//    撰寫現場評論            → /reviews/visit/:id/write        （id ＝ 現場評論憑證 ReviewPerVisitId）
//    撰寫預約評論            → /reviews/booking/:orderId/write
//    查看我的現場評論        → /reviews/visit/:id
//    查看我的預約評論        → /reviews/booking/:orderId
//    員工預覽                → /reviews/preview/:reviewId
//
//  ── 10/9 現場評論改限會員 ─────────────────────────────
//  網址從 QRToken 改成憑證 Id，兩種評論都要會員本人登入（後端用 JWT 擋）。
//  這幾頁不在 /member 底下，組裡的路由守衛不會先擋；沒登入時後端回 401，頁面顯示「請先登入」。
//  :id(\d+) 表示只接受數字，網址打錯（例如舊的 QRToken 連結）會直接進 404 頁。
//
//  ── 為什麼兩種撰寫頁共用一個 View ─────────────────────
//  Razor 時期分成兩個 .cshtml，是因為兩者的 ViewModel 型別不同
//  （ReviewCreateForVisitVM／ReviewCreateForBookingVM）。
//  改成 API 之後，兩者的差別全部由後端回傳的 form 設定決定
//  （要不要顯示提及標籤、能不能選匿名⋯），畫面本身只有一份。
//  改一個地方兩邊一起生效，不會再出現「現場版改了、預約版忘了改」。
//
//  ── 為什麼用 props 接參數，不在元件裡讀 route.params ──
//  元件只知道「我拿到 kind 和 ticket」，不知道它們從網址哪一段來。
//  之後網址格式要改，只改這個檔案，元件不用動。
//
//  ⚠️ 用「動態 import」載入元件（() => import(...)）：
//     使用者沒進到那一頁就不會下載那一頁的程式碼。
// ============================================================

const ReviewIndexView = () => import("@/views/review/ReviewIndexView.vue");
const ReviewWriteView = () => import("@/views/review/ReviewWriteView.vue");
const MyReviewView = () => import("@/views/review/MyReviewView.vue");

export default [
  {
    path: "/reviews",
    name: "review-index",
    component: ReviewIndexView,
    meta: { title: "評論專區" },
  },

  // ── 撰寫 ────────────────────────────────────────────
  {
    // 現場評論：憑證 Id，要會員本人登入
    path: "/reviews/visit/:id(\\d+)/write",
    name: "review-write-visit",
    component: ReviewWriteView,
    props: (route) => ({ kind: "visit", ticket: String(route.params.id) }),
    meta: { title: "撰寫評論", requiresMember: true },
  },
  {
    // 預約評論：要會員本人登入。
    // requiresMember 只是「標記」：組裡的路由守衛只擋 /member 開頭的網址，不看這個標記。
    // 就算前端不擋，後端也會擋（JWT＋Member 角色）。
    path: "/reviews/booking/:orderId/write",
    name: "review-write-booking",
    component: ReviewWriteView,
    props: (route) => ({ kind: "booking", ticket: String(route.params.orderId) }),
    meta: { title: "撰寫評論", requiresMember: true },
  },

  // ── 檢視自己的評論 ──────────────────────────────────
  {
    path: "/reviews/visit/:id(\\d+)",
    name: "review-mine-visit",
    component: MyReviewView,
    props: (route) => ({ kind: "visit", ticket: String(route.params.id) }),
    meta: { title: "我的評論", requiresMember: true },
  },
  {
    path: "/reviews/booking/:orderId",
    name: "review-mine-booking",
    component: MyReviewView,
    props: (route) => ({ kind: "booking", ticket: String(route.params.orderId) }),
    meta: { title: "我的評論", requiresMember: true },
  },

  // ── 員工預覽（從館方清單開過來）──────────────────────
  //  跟「我的評論」同一個畫面，所以員工看到的就是顧客看到的。
  //  差別只在：資料改從員工專用的 API 拿、所有按鈕停用、不記錄已讀。
  //  網址用 reviewId 沒關係：這支 API 只有後台角色打得開（見規格 2-8）。
  {
    path: "/reviews/preview/:reviewId",
    name: "review-preview",
    component: MyReviewView,
    props: (route) => ({ previewId: String(route.params.reviewId) }),
    meta: { title: "顧客視角預覽", requiresBackOffice: true },
  },
];

/**
 * 依種類組出「撰寫頁」或「檢視頁」的路由位置。
 * 頁面之間互相跳轉都走這兩個函式，網址規則只寫在這個檔案。
 */
export function writeRoute(kind, ticket) {
  if (kind === "visit") return { name: "review-write-visit", params: { id: ticket } };
  if (kind === "booking") return { name: "review-write-booking", params: { orderId: ticket } };
  throw new Error(`[reviewRoutes] 不認得的評論種類：${kind}`);
}

export function mineRoute(kind, ticket) {
  if (kind === "visit") return { name: "review-mine-visit", params: { id: ticket } };
  if (kind === "booking") return { name: "review-mine-booking", params: { orderId: ticket } };
  throw new Error(`[reviewRoutes] 不認得的評論種類：${kind}`);
}
