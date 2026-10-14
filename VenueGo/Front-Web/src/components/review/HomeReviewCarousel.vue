<!--
  HomeReviewCarousel.vue — 首頁「顧客評價」區塊（昱，10/13）
  用法（HomeView.vue）：<HomeReviewCarousel />

  ── 內容 ─────────────────────────────────────────────────────
  標題＋整體平均分數，下面是最新的公開評論卡片（單列輪播，版型仿首頁的「場地介紹」）。
  資料用評論專區同一支 API（規格 2-1），上架條件只有一份，首頁不會出現評論專區沒有的評論。

  ── 為什麼這樣抓資料 ─────────────────────────────────────────
  1. range=all：平均分數要代表「全部」的評價，不是近一年。
  2. hasContentOnly=false 再在前端濾掉「只給星等」的：
     如果在後端只抓有文字的，平均分數也會只算有文字的評論，跟評論專區對不上。
  3. take=30 再取前 12 則有文字的：最新的 30 則裡難免有只給星等的，多抓一些才湊得滿 12 則。
  4. 不挑星等：首頁顯示的就是最新的真實評價（已經過館方審核、下架的不會出現）。
     只挑高分的看起來漂亮，但顧客點進評論專區一看就知道，反而傷信任。

  ── 10/14：無限循環 ─────────────────────────────────────────
  5. loop：往左、往右都沒有盡頭，最後一張接回第一張（Embla 把最前面的卡片移到後面接上，不是複製一份）。
     張數不夠循環時（不到「一頁＋1 張」），Embla 會自動當成一般輪播。
  6. 12 張：手機一頁 1 張、平板 2 張、電腦 3 張，12 都整除，每種寬度的最後一頁都是滿的，
     接回第一頁時不會出現半頁空白。
  7. 分頁點點（平板以上）：循環之後沒有「最後一頁」，用點點告訴使用者看到第幾頁。手機 12 個點太密，不顯示。
  8. 卡片本身不能點；評論或回覆被截斷時，卡片裡才有「閱讀全文」，開中央視窗看（ReviewCard 檔頭第 6 點）。
-->
<script setup>
import { computed, onMounted, ref } from "vue";
import { RouterLink } from "vue-router";
import { getPublicReviews } from "@/api/reviewApi";
import ReviewCard from "@/components/review/ReviewCard.vue";
import StarView from "@/components/review/StarView.vue";
import IconChevronLeftBold from "@/components/icons/IconChevronLeftBold.vue";
import IconChevronRightBold from "@/components/icons/IconChevronRightBold.vue";

const SHOW_COUNT = 12; // 輪播最多幾張（理由見檔頭第 6 點）
const FETCH_COUNT = 30; // 跟後端要幾則（理由見檔頭第 3 點）

const vm = ref(null);
const isLoading = ref(true);
const errorMessage = ref("");

onMounted(async () => {
  try {
    vm.value = await getPublicReviews({
      range: "all",
      sort: "newest",
      hasContentOnly: false,
      take: FETCH_COUNT,
    });
  } catch (error) {
    // http.js 已經把錯誤轉成中文訊息
    errorMessage.value = error.message;
  } finally {
    isLoading.value = false;
  }
});

const cards = computed(() =>
  (vm.value?.items ?? []).filter((item) => !item.isRatingOnly).slice(0, SHOW_COUNT),
);

// 平均分數顯示一位小數（4.666… → 4.7）；四捨五入後的整數星等給星星用
const averageText = computed(() => (vm.value ? vm.value.summary.average.toFixed(1) : ""));
const averageStars = computed(() => (vm.value ? Math.round(vm.value.summary.average) : 0));

// 只有這個區塊長這樣，寫在這裡的 :ui，不改全站設定（跟 VenueCarousel 同一套）
const carouselUi = {
  container: "items-stretch",
  // 手機 85%（露出下一張邊緣，提示可以滑動）、平板 2 張、電腦 3 張（評論字多，比場地卡寬一點）
  item: "basis-[85%] md:basis-1/2 lg:basis-1/3",
  // 箭頭跟場地卡不同：評論卡是文字，箭頭放在卡片內側會蓋住內容，所以移到卡片外側（壓在版面左右的留白上）。
  // 平板以下用滑的、不顯示箭頭：那時左右留白不夠放。左右都不能按（張數不超過一頁）時整組隱藏。
  arrows: "max-lg:hidden has-[[data-slot=prev]:disabled]:has-[[data-slot=next]:disabled]:hidden",
  prev: "-start-5 sm:-start-5 rounded text-neutral-text-primary",
  next: "-end-5 sm:-end-5 rounded text-neutral-text-primary",
  // 10/14：分頁點點（檔頭第 7 點）。目前這一頁用品牌藍
  dots: "max-md:hidden",
  dot: "size-2.5 data-[state=active]:bg-brand-primary",
};
</script>

<template>
  <section>
    <!-- 區塊標題（首頁的 h1 在 HomeView）＋「看全部」 -->
    <div class="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
      <div class="flex items-center gap-2">
        <UIcon name="i-lucide-message-square-quote" class="size-6 text-brand-accent" />
        <h2 class="text-2xl font-bold text-neutral-text-primary">顧客評價</h2>
      </div>
      <RouterLink
        :to="{ name: 'review-index' }"
        class="inline-flex items-center gap-1 text-sm font-semibold text-brand-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
      >
        看全部評價
        <IconChevronRightBold class="size-4" aria-hidden="true" />
      </RouterLink>
    </div>

    <!-- 載入中：卡片骨架，張數跟輪播一樣（手機 1、平板 2、電腦 3） -->
    <div v-if="isLoading" class="mt-6 grid gap-4 md:grid-cols-2 lg:grid-cols-3">
      <USkeleton
        v-for="n in 3"
        :key="n"
        class="h-56 rounded-xl"
        :class="{ 'hidden md:block': n === 2, 'hidden lg:block': n === 3 }"
      />
    </div>

    <!-- 載入失敗：只影響這個區塊，首頁其他區塊照常顯示 -->
    <p
      v-else-if="errorMessage"
      class="mt-6 rounded-lg border border-semantic-error bg-neutral-surface p-4 text-sm text-semantic-error"
    >
      評價暫時無法載入：{{ errorMessage }}
    </p>

    <p v-else-if="cards.length === 0" class="mt-6 text-sm text-neutral-text-secondary">
      還沒有公開的評價
    </p>

    <template v-else>
      <!-- 整體分數：大數字＋星星＋則數 -->
      <p class="mt-3 flex flex-wrap items-center gap-x-3 gap-y-1">
        <span class="text-3xl font-bold text-neutral-text-primary tabular-nums">{{
          averageText
        }}</span>
        <StarView :rating="averageStars" size-class="text-lg" />
        <span class="text-sm text-neutral-text-secondary">共 {{ vm.summary.total }} 則評價</span>
      </p>

      <UCarousel
        v-slot="{ item }"
        :items="cards"
        align="start"
        slides-to-scroll="auto"
        loop
        arrows
        dots
        :prev-icon="IconChevronLeftBold"
        :next-icon="IconChevronRightBold"
        :ui="carouselUi"
        class="mt-4 md:mb-10"
      >
        <ReviewCard :item="item" />
      </UCarousel>
    </template>
  </section>
</template>
