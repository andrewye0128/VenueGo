import { createRouter, createWebHistory } from "vue-router";
import HomeView from "../views/HomeView.vue";
import { useMemberStore } from "@/stores/member";
// 顧客端評論的路由（昱）：評論專區、撰寫、我的評論、員工預覽
import reviewRoutes from "./reviewRoutes";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: "/",
      name: "home",
      component: HomeView,
    },
    {
      path: "/about",
      name: "about",
      // route level code-splitting
      // this generates a separate chunk (About.[hash].js) for this route
      // which is lazy-loaded when the route is visited.
      component: () => import("../views/AboutView.vue"),
    },
    {
      path: "/booking",
      name: "booking",
      component: () => import("../views/BookingView.vue"),
    },
    {
      // 場地預約 ② 填寫資料：/booking/confirm?venueId=7&date=2026-10-20&slots=17:00,18:00
      path: "/booking/confirm",
      name: "booking-confirm",
      component: () => import("../views/BookingConfirmView.vue"),
      meta: { requiresAuth: true },
    },
    {
      // 場地預約 ③ 預約完成：資料由填寫頁透過 history state 帶過來（不放網址）
      path: "/booking/complete",
      name: "booking-complete",
      component: () => import("../views/BookingCompleteView.vue"),
      meta: { requiresAuth: true },
    },
    {
      path: "/login",
      name: "login",
      component: () => import("../views/LoginView.vue"),
    },
    {
      path: "/register",
      name: "register",
      component: () => import("../views/RegisterView.vue"),
    },
    {
      path: "/forgot-password",
      name: "forgot-password",
      component: () => import("../views/ForgotPasswordView.vue"),
    },
    {
      path: "/reset-password",
      name: "reset-password",
      component: () => import("../views/ResetPasswordView.vue"),
    },
    {
      // SportTypeSelect 元件使用說明頁
      path: "/demo/select",
      name: "demo-select",
      component: () => import("../views/SelectDemoView.vue"),
    },
    {
      // UButton 使用說明頁
      path: "/demo/button",
      name: "demo-button",
      component: () => import("../views/ButtonDemoView.vue"),
    },
    {
      // 前端驗證（UForm + Zod）範例頁
      path: "/demo/form",
      name: "demo-form",
      component: () => import("../views/FormDemoView.vue"),
    },
    {
      // 前後端 API 連線測試頁
      path: "/demo/api",
      name: "demo-api",
      component: () => import("../views/ApiDemoView.vue"),
    },
    {
      path: "/member",
      name: "member",
      component: () => import("../layouts/member/MemberLayout.vue"),
      // 需要登入：底下的子頁面（我的預約、個人資料…）也一起適用
      meta: { requiresAuth: true },
      children: [
        // 進入會員中心時，預設顯示個人資料
        {
          path: "member-home",
          redirect: { name: "profile" },
        },
        // 我的預約
        {
          path: "reservations",
          name: "reservations",
          component: () => import("../views/member/MyReservationsView.vue"),
        },
        // 我的票券
        {
          path: "tickets",
          name: "tickets",
          component: () => import("../views/member/MyTicketsView.vue"),
        },
        // 個人資料
        {
          path: "profile",
          name: "profile",
          component: () => import("../views/member/MemberProfileView.vue"),
        },
        // 我的評論（昱，10/10）：待評論／已評論
        {
          path: "reviews",
          name: "member-reviews",
          component: () => import("../views/member/MyReviewsView.vue"),
        },
      ],
    },
    {
      // 場館資訊：/venues/1 顯示運動類型 Id 1；不帶 Id 時由頁面導到第一項
      // (\\d+) 限定只接受數字，避免吃掉 /venues/transport 之類的其他頁面
      path: "/venues/:sportTypeId(\\d+)?",
      name: "venues",
      component: () => import("../views/VenueIntroView.vue"),
    },
    {
      // 最新消息列表：分類、頁碼放在查詢字串（/news?category=event&page=2）
      path: "/news",
      name: "news",
      component: () => import("../views/news/NewsListView.vue"),
    },
    {
      // 最新消息詳細頁：(\\d+) 限定只接受數字，非數字的網址交給 404
      path: "/news/:id(\\d+)",
      name: "news-detail",
      component: () => import("../views/news/NewsDetailView.vue"),
    },
    {
      // 常見問題：目前的分類放在查詢字串（/faq?category=venue），沒帶時顯示第一個分類
      path: "/faq",
      name: "faq",
      component: () => import("../views/faq/FaqView.vue"),
    },
    // 顧客端評論（昱）：要放在 404 前面
    ...reviewRoutes,
    {
      // 404：網址不符合上面任何路由時顯示（一定要放在最後一個）
      path: "/:pathMatch(.*)*",
      name: "not-found",
      component: () => import("../views/NotFoundView.vue"),
    },
  ],
  // 換頁時的捲動位置（昱）。沒有這段的話，vue-router 換頁不會捲動：
  // 在上一頁捲到下面，進到新的一頁還是停在下面。
  //   按瀏覽器上一頁／下一頁 → 回到當時捲到的位置
  //   同一頁只換查詢字串（例如評論專區切換篩選）→ 不要捲動，不然每點一個篩選畫面就跳回最上面
  //   其餘換頁 → 回到最上面
  scrollBehavior(to, from, savedPosition) {
    if (savedPosition) return savedPosition;
    if (to.path === from.path) return false;
    return { top: 0 };
  },
});
// 路由守衛：未登入或 Token 無效時，禁止進入需要登入的頁面。
// 哪些頁面要登入：路由設定加上 meta: { requiresAuth: true }（子頁面會跟著父頁面）
//   目前有：會員中心 /member/...、場地預約的填寫資料、預約完成
// 帶去登入頁時，把原本要去的網址放在 ?redirect=，登入成功後 LoginView 會帶回來
router.beforeEach(async (to) => {
  // to.matched：這個網址對應到的所有路由（含父路由），任一個要求登入就算
  if (!to.matched.some((record) => record.meta.requiresAuth)) return;

  const token = localStorage.getItem("token");
  const memberStore = useMemberStore();
  // fullPath 含查詢字串，例如 /booking/confirm?venueId=7&date=...，選擇才不會遺失
  const toLogin = { name: "login", query: { redirect: to.fullPath } };

  if (!token) {
    return toLogin;
  }

  if (!memberStore.member) {
    try {
      await memberStore.fetchCurrentMember();
    } catch (error) {
      memberStore.logout();
      return toLogin;
    }
  }
});

export default router;
