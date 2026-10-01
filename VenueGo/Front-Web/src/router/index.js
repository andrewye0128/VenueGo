import { createRouter, createWebHistory } from "vue-router";
import HomeView from "../views/HomeView.vue";
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
      // 場館資訊：/venues/1 顯示運動類型 Id 1；不帶 Id 時由頁面導到第一項
      // (\\d+) 限定只接受數字，避免吃掉 /venues/transport 之類的其他頁面
      path: "/venues/:sportTypeId(\\d+)?",
      name: "venues",
      component: () => import("../views/VenueIntro.vue"),
    },
    {
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

export default router;
