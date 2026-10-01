import { createRouter, createWebHistory } from "vue-router";
import HomeView from "../views/HomeView.vue";

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
      path: "/member",
      name: "member",
      component: () => import("../views/member/MemberLayout.vue"),
      children: [
        // 我的票券
        {
          path: "tickets",
          name: "tickets",
          component: () => import("../views/member/MyTicketsView.vue"),
        },
      ],
    },
    {
      // 404：網址不符合上面任何路由時顯示（一定要放在最後一個）
      path: "/:pathMatch(.*)*",
      name: "not-found",
      component: () => import("../views/NotFoundView.vue"),
    },
  ],
});

export default router;
