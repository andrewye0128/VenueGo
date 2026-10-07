<!-- 會員中心版面：電腦版左側選單＋右側內容；手機、平板版選單改成上方可左右滑動的分頁列 -->
<!-- UIcon 由 Nuxt UI 自動匯入 -->
<script setup>
import { nextTick, onMounted, ref, watch } from "vue";
import { RouterLink, RouterView, useRoute } from "vue-router";

const route = useRoute();

// 網址用路徑字串：頁面還沒做好時會自動顯示 404；組員做好頁面、路由加上同樣的路徑後，這裡不用改
// 圖示名稱要寫完整，Nuxt UI 打包時才掃描得到
const sideMenuItems = [
  { label: "個人資料", to: "/member/profile", icon: "i-mdi-card-account-details-outline" },
  { label: "我的預約", to: "/member/reservations", icon: "i-mdi-calendar-month-outline" },
  { label: "訂單與付款", to: "/member/orders", icon: "i-mdi-credit-card-outline" },
  { label: "我的票券", to: "/member/tickets", icon: "i-mdi-ticket-confirmation-outline" },
  { label: "我的評論", to: "/member/reviews", icon: "i-mdi-star-outline" },
];

// 假資料：登入功能完成後，改成讀取登入的會員資料
const user = {
  name: "王小明",
  email: "member@example.com",
  avatar: null,
};

// ── 手機、平板：把目前頁的分頁捲到分頁列中間 ──
// 例如進入「我的評論」時，它在分頁列最右邊，可能被擠到畫面外
// 只捲動分頁列本身（scrollTo），不用 scrollIntoView，避免連整個頁面一起捲動
// 電腦版分頁列不會左右捲動，這段不會有任何效果
const navRef = ref(null);

function scrollActiveTabIntoView() {
  const nav = navRef.value;
  // is-active：寫在下方 active-class 裡的標記
  // （設定 active-class 之後，RouterLink 就不會再加預設的 router-link-active，所以要自己加標記）
  const active = nav?.querySelector(".is-active");
  if (!active) return;
  nav.scrollTo({ left: active.offsetLeft - (nav.clientWidth - active.offsetWidth) / 2 });
}

onMounted(scrollActiveTabIntoView);
watch(
  () => route.path,
  () => nextTick(scrollActiveTabIntoView),
);
</script>

<template>
  <div class="bg-brand-background">
    <!-- 寬度跟 NavBar、Footer 一致：max-w-7xl + px-4 md:px-6，各尺寸左右邊界都對齊 -->
    <div class="mx-auto max-w-7xl px-4 py-6 md:px-6 lg:flex lg:items-start lg:gap-6">
      <!-- 會員卡片：手機、平板在上方；電腦在左側 -->
      <aside class="rounded border border-neutral-border bg-neutral-surface lg:w-56 lg:shrink-0">
        <!-- 會員資訊 -->
        <div class="flex items-center gap-3 border-b border-neutral-border p-4 lg:gap-4">
          <div
            class="flex size-10 shrink-0 items-center justify-center rounded-full bg-brand-background text-neutral-text-primary ring ring-neutral-border lg:size-14"
          >
            <UIcon name="i-mdi-account-outline" class="size-6 lg:size-8" />
          </div>
          <div class="min-w-0">
            <p class="font-semibold text-neutral-text-primary">{{ user.name }}</p>
            <p class="truncate text-xs text-neutral-text-secondary">{{ user.email }}</p>
          </div>
        </div>

        <!-- 選單：手機、平板橫排（放不下時左右滑動，目前頁下方藍線）；電腦直排（目前頁淡藍底＋左側藍線）
             active-class：目前頁的樣式，寫法跟 NavBar.vue 一樣；「!」讓它蓋過一般狀態的顏色
             目前頁的判斷是「網址開頭符合」，之後做 /member/reservations/12 這類子頁面時，「我的預約」一樣會標示
             手機版不用 -mb-px 讓藍線往下凸，避免分頁列多出 1px 而出現直向小捲軸 -->
        <nav
          ref="navRef"
          aria-label="會員中心"
          class="relative flex overflow-x-auto px-2 lg:flex-col lg:overflow-visible lg:px-0 lg:py-2"
        >
          <RouterLink
            v-for="item in sideMenuItems"
            :key="item.label"
            :to="item.to"
            class="flex shrink-0 items-center gap-3 border-b-2 border-transparent px-4 py-3 text-sm text-neutral-text-secondary transition-colors hover:text-brand-primary focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-brand-primary lg:border-b-0 lg:border-l-3 lg:hover:bg-brand-primary/8"
            active-class="is-active !border-brand-primary font-semibold !text-brand-primary lg:!bg-brand-primary/8"
          >
            <UIcon :name="item.icon" class="hidden size-5 lg:block" />
            {{ item.label }}
          </RouterLink>
        </nav>
      </aside>

      <!-- 內容：min-w-0 讓寬表格可以在內容區裡左右捲動，不會把整個版面撐寬 -->
      <main class="mt-4 min-w-0 flex-1 lg:mt-0">
        <RouterView />
      </main>
    </div>
  </div>
</template>
