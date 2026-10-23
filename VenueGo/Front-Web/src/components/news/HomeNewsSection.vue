<script setup>
// 首頁「最新消息」區塊：最新 8 則消息的圖卡輪播 + 「看更多」連結到列表頁
// UButton、UCarousel、UIcon、USkeleton 由 Nuxt UI 自動匯入
import { onMounted, ref } from "vue";
import { getNewsList } from "@/api/newsApi";
import NewsCard from "@/components/news/NewsCard.vue";
import IconChevronLeftBold from "@/components/icons/IconChevronLeftBold.vue";
import IconChevronRightBold from "@/components/icons/IconChevronRightBold.vue";

// 首頁顯示 8 則（電腦一次 4 張，按箭頭看另外 4 張）：同一天發布多則消息時也不容易被擠掉
// 跟列表頁共用同一支 API，用 pageSize 控制筆數
const HOME_NEWS_COUNT = 8;

// 載入中骨架的張數 = 電腦版一次顯示的張數
const SKELETON_COUNT = 4;

// 首頁封面：1:1 圖框，4:5 海報完整縮放放在中間（不裁切）
// 四周 p-3 留底色當襯框，海報下緣的白底才不會跟文字區的白底連成一片
const HOME_COVER_CLASS = "aspect-square object-contain bg-brand-background p-3";

const newsList = ref([]);
const isLoading = ref(true);
const errorMessage = ref("");

onMounted(async () => {
  try {
    const result = await getNewsList({ pageSize: HOME_NEWS_COUNT });
    // 首頁不換頁，只需要 items
    newsList.value = result.items;
  } catch (error) {
    // http.js 已經把錯誤轉成中文訊息
    errorMessage.value = error.message;
  } finally {
    isLoading.value = false;
  }
});

// 輪播外觀：只用 Tailwind 標準 class（前台 CSS 原則：不用任意值、不用 !important）
const carouselUi = {
  // 卡片等高
  container: "items-stretch",
  // 一次顯示幾張：跟場地介紹（VenueCarousel）一致
  // 手機 85%（露出下一張邊緣，提示可以滑動）、平板 2 張、電腦 4 張
  item: "basis-[85%] md:basis-1/2 lg:basis-1/4",
  // 箭頭樣式跟首頁評價輪播（HomeReviewCarousel）一致：
  // 平板以下用滑的、不顯示箭頭（左右留白不夠放）；左右都不能按（張數不超過一頁）時整組隱藏
  arrows: "max-lg:hidden has-[[data-slot=prev]:disabled]:has-[[data-slot=next]:disabled]:hidden",
  // 箭頭放在卡片外側（壓在版面左右的留白上），不蓋住卡片內容
  prev: "-start-5 sm:-start-5 rounded text-neutral-text-primary",
  next: "-end-5 sm:-end-5 rounded text-neutral-text-primary",
};
</script>

<template>
  <section>
    <!-- 區塊標題：h2（首頁的 h1 在 HomeView）；右邊「看更多」連到列表頁 -->
    <div class="flex items-center justify-between gap-2">
      <div class="flex items-center gap-2">
        <UIcon name="i-mdi-newspaper-variant-outline" class="size-6 text-brand-accent" />
        <h2 class="text-2xl font-bold text-neutral-text-primary">最新消息</h2>
      </div>
      <UButton color="primary" variant="link" trailing-icon="i-mdi-chevron-right" to="/news">
        看全部消息
      </UButton>
    </div>

    <!-- 載入中：灰色卡片骨架，張數跟輪播一次顯示的一樣（手機 1、平板 2、電腦 4） -->
    <!-- 骨架高度 h-111（444px）＝電腦版卡片高度（1:1 封面 + 文字區），載入完畫面才不會跳動 -->
    <div v-if="isLoading" class="mt-6 grid gap-4 md:grid-cols-2 lg:grid-cols-4">
      <USkeleton
        v-for="n in SKELETON_COUNT"
        :key="n"
        class="h-111 rounded-lg"
        :class="{ 'hidden md:block': n === 2, 'hidden lg:block': n > 2 }"
      />
    </div>

    <!-- 載入失敗：只影響這個區塊，首頁其他區塊照常顯示 -->
    <p
      v-else-if="errorMessage"
      class="mt-6 rounded-lg border border-semantic-error bg-neutral-surface p-4 text-sm text-semantic-error"
    >
      最新消息暫時無法載入：{{ errorMessage }}
    </p>

    <p v-else-if="newsList.length === 0" class="mt-6 text-sm text-neutral-text-secondary">
      目前沒有最新消息
    </p>

    <!-- align="start"：第一張靠左對齊；slides-to-scroll="auto"：按一次箭頭換一整組 -->
    <UCarousel
      v-else
      v-slot="{ item }"
      :items="newsList"
      align="start"
      slides-to-scroll="auto"
      arrows
      :prev-icon="IconChevronLeftBold"
      :next-icon="IconChevronRightBold"
      :ui="carouselUi"
      class="mt-6"
    >
      <NewsCard :news="item" :cover-class="HOME_COVER_CLASS" />
    </UCarousel>
  </section>
</template>
