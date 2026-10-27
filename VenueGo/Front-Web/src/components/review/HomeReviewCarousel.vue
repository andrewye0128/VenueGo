<!--
  HomeReviewCarousel.vue — 首頁「顧客評價」區塊（昱，10/13）
  用法（HomeView.vue）：<HomeReviewCarousel />

  ── 內容 ─────────────────────────────────────────────────────
  標題＋整體平均分數，下面是挑選過的公開評論卡片（單列輪播，版型仿首頁的「場地介紹」）。

  ── 資料（10/15 改成專用 API，規格 2-11）──────────────────────
  1~4.（10/13 版：用評論專區的 API 抓最新 30 則、前端濾掉只給星等的。10/15 起不用了。）
  9. 卡片由後端挑：近 3 個月 5 星 → 近 3 個月 4 星 → 近 1 年 5 星 → 近 1 年 4 星 → 全部 3 星以上，
     某一階段放不下時從那一階段隨機抽，最多 12 則，順序也打亂。每次打開首頁看到的都不一樣。
     上架條件跟評論專區是同一份（後端 PublicReviewQueryService），首頁不會出現評論專區沒有的評論。
     平均分數、總則數是全部公開評價（含只給星等的），跟評論專區「全部時間」一致。
  10. 10/16：挑選規則改版（有館方回覆、30 字以上的優先，最後才用只給星等的），細節在後端 PickFeatured。
      一則都挑不到時，整個區塊拿掉（連標題都不顯示），不再顯示「還沒有公開的評價」。
      載入失敗還是顯示錯誤訊息：那是「出問題了」，不是「沒有評價」。

  ── 10/14：無限循環 ─────────────────────────────────────────
  5. loop：往左、往右都沒有盡頭，最後一張接回第一張（Embla 把最前面的卡片移到後面接上，不是複製一份）。
     張數不夠循環時（不到「一頁＋1 張」），Embla 會自動當成一般輪播。
  6. 12 張：手機一頁 1 張、平板 2 張、電腦 3 張，12 都整除，每種寬度的最後一頁都是滿的，
     接回第一頁時不會出現半頁空白。
  7. 位置提示：4 條短橫桿，每條代表 3 則（第 1 條＝第 1～3 則、第 2 條＝第 4～6 則⋯）。
     按第 1 條，第 2 則移到正中間，第 1～3 則同時看得到；按第 2 條，第 5 則移到正中間，以此類推。
     為了做到「按了之後那 3 則剛好都在畫面上」，卡片改成置中對齊（align=center），一開始停在第 2 則。
     （10/14 晚：原本 12 個點點太密拿掉了；完全沒有提示又看不出自己在哪，所以改成 4 條。）
  8. 10/14 晚：滑動改成「像拉霸機」——
     - slides-to-scroll=1：每一張都是停靠點，可以停在任何一張，不再一次固定三張。
     - skip-snaps：用力甩的時候會帶著慣性連續滑過好幾張，減速後停在某一張；輕輕拖就只移一張。
     - 箭頭一次移一張。
     不用 drag-free（完全自由滑動）：它會停在兩張卡片中間，卡片被切一半，看起來像沒滑好。
  9. 卡片本身不能點；評論或回覆被截斷時，卡片裡才有「閱讀全文」，開中央視窗看（ReviewCard 檔頭第 6 點）。
-->
<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { RouterLink } from "vue-router";
import { getFeaturedReviews } from "@/api/reviewApi";
import ReviewCard from "@/components/review/ReviewCard.vue";
import StarView from "@/components/review/StarView.vue";
import IconChevronLeftBold from "@/components/icons/IconChevronLeftBold.vue";
import IconChevronRightBold from "@/components/icons/IconChevronRightBold.vue";

const vm = ref(null);
const isLoading = ref(true);
const errorMessage = ref("");

onMounted(async () => {
  try {
    vm.value = await getFeaturedReviews(); // 檔頭第 9 點
  } catch (error) {
    // http.js 已經把錯誤轉成中文訊息
    errorMessage.value = error.message;
  } finally {
    isLoading.value = false;
  }
});

// 後端已經挑好、打亂順序（最多 12 則），照順序顯示
const cards = computed(() => vm.value?.items ?? []);

// 平均分數顯示一位小數（4.666… → 4.7）；四捨五入後的整數星等給星星用
// ── 位置提示（檔頭第 7 點）──
const GROUP_SIZE = 3;
const carouselRef = ref(null);
const selected = ref(1); // 目前置中的是第幾張（從 0 算）。一開始停在第 2 張（start-index）
const groupCount = computed(() => Math.ceil(cards.value.length / GROUP_SIZE));
const activeGroup = computed(() => Math.floor(selected.value / GROUP_SIZE));
// 卡片不到 2 張時從第 1 張開始，不然會停在不存在的第 2 張
const startIndex = computed(() => (cards.value.length >= 2 ? 1 : 0));
watch(startIndex, (i) => (selected.value = i), { immediate: true });

function groupLabel(g) {
  const from = g * GROUP_SIZE + 1;
  const to = Math.min(from + GROUP_SIZE - 1, cards.value.length);
  return from === to ? `第 ${from} 則` : `第 ${from}～${to} 則`;
}

// 把那一組中間那張移到正中間；最後一組不滿 3 張時，取那一組實際有的中間
function goToGroup(g) {
  const from = g * GROUP_SIZE;
  const to = Math.min(from + GROUP_SIZE, cards.value.length) - 1;
  carouselRef.value?.emblaApi?.scrollTo(Math.floor((from + to) / 2));
}

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
};
</script>

<template>
  <!-- 檔頭第 10 點：挑不到評價就整個區塊不顯示 -->
  <section v-if="isLoading || errorMessage || cards.length > 0">
    <!-- 區塊標題（首頁的 h1 在 HomeView）＋「看全部」
         10/17：items-center → items-baseline，「看全部評價」的字跟「顧客評價」的字對齊同一條底線 -->
    <div class="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-2">
      <div class="flex items-baseline gap-2">
        <UIcon
          name="i-lucide-message-square-quote"
          class="size-6 shrink-0 self-center text-brand-accent"
          aria-hidden="true"
        />
        <h2 class="text-2xl font-bold text-neutral-text-primary">顧客評價</h2>
      </div>
      <RouterLink
        :to="{ name: 'review-index' }"
        class="inline-flex items-center gap-1 text-base font-semibold text-brand-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
      >
        看全部評價
        <IconChevronRightBold class="size-4 self-center" aria-hidden="true" />
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
        ref="carouselRef"
        align="center"
        :start-index="startIndex"
        :slides-to-scroll="1"
        skip-snaps
        loop
        arrows
        :prev-icon="IconChevronLeftBold"
        :next-icon="IconChevronRightBold"
        :ui="carouselUi"
        class="mt-4"
        @select="(i) => (selected = i)"
      >
        <ReviewCard :item="item" featured />
      </UCarousel>

      <!-- 位置提示：4 條短橫桿，每條 3 則（檔頭第 7 點）。只有 1 組時不顯示 -->
      <div
        v-if="groupCount > 1"
        role="group"
        aria-label="評價位置"
        class="mt-3 flex items-center justify-center gap-1"
      >
        <!-- 看得到的橫桿只有 6px 高，按鈕本身留 p-2，手指比較好按 -->
        <button
          v-for="g in groupCount"
          :key="g"
          type="button"
          class="group cursor-pointer rounded p-2 focus-visible:outline-2 focus-visible:outline-brand-primary"
          :aria-label="`看${groupLabel(g - 1)}`"
          :aria-current="activeGroup === g - 1 ? 'true' : undefined"
          @click="goToGroup(g - 1)"
        >
          <span
            class="block h-1.5 rounded-full transition-all duration-300"
            :class="
              activeGroup === g - 1
                ? 'w-8 bg-brand-primary'
                : 'w-4 bg-neutral-border group-hover:bg-brand-primary/40'
            "
          ></span>
        </button>
      </div>
    </template>
  </section>
</template>
