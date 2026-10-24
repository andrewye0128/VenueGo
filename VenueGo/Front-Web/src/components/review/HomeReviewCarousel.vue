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
  ── 10/23：箭頭可以長按 ──────────────────────────────────────
  11. 按住箭頭：0.5 秒後開始，每 0.5 秒再換一張（跟評論專區的換頁一樣，composables/useRepeatPress.js）。
      10/24 改成「拉霸」：按住 0.2 秒後開始，每 0.1 秒換一張，連續快速轉動，跟按住鍵盤 ← → 的感覺一樣。
      評論專區按住時有遮罩告訴你「正在快轉」，這裡沒有，0.8 秒才開始動會讓人以為沒反應；
      而且這個輪播本來就要給人「抽獎」的感覺，轉的時候看不清楚沒關係，停下來那張才是重點。
      箭頭沿用 UCarousel 內建的那兩顆，外觀跟其他輪播（例如場地介紹）完全一樣，只是在它們身上加了長按的監聽。
  ── 10/24：箭頭「亮起來」要跟實際方向一致 ─────────────────────
  12. 原本用滑鼠點了左箭頭，焦點會留在左箭頭上；接著按鍵盤 →，瀏覽器判斷「現在用鍵盤了」，
      就在左箭頭上畫出藍色焦點框——看起來像左邊亮了，實際卻往右轉；放開按鍵之後框也一直留著。
      - 用滑鼠、手指按箭頭時，不讓箭頭拿到焦點，改成讓整個輪播拿到焦點（UCarousel 的外框本來就可以拿焦點、
        也沒有焦點框）。← → 照樣能換張，箭頭也不會冒出藍框。
      - 按住鍵盤 ← 時左箭頭亮、→ 時右箭頭亮，放開就恢復；用滑鼠、手指按住箭頭時也一樣亮著。
      - 用 Tab 移到箭頭上的人（只用鍵盤的人），焦點框照常顯示：他們要靠這個框知道自己在哪。
-->
<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useRepeatPress } from "@/composables/useRepeatPress";
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

// ── 箭頭長按（檔頭第 11 點）──
//  按一下：交給內建的 click 換一張，跟原本一模一樣。
//  按住：一段時間後由這裡連續換（速度見下面的 SPIN_）。放開時瀏覽器還會補送一次 click，
//        那一次要吃掉，不然放開的瞬間會多換一張。
//  內建的箭頭沒有開放「長按」的設定，所以等輪播畫出來之後，直接在那兩顆按鈕上加監聽
//  （它們的 data-slot 是 prev／next，這是 Nuxt UI 原始碼裡固定的名稱）。
const sectionEl = ref(null);
let swallowClick = false;
// 10/24：拉霸的速度（檔頭第 11 點）。評論專區的換頁維持原本的 0.3 秒＋每 0.5 秒
const SPIN_HOLD_MS = 200;
const SPIN_INTERVAL_MS = 100;
const arrowPress = useRepeatPress({
  holdDelay: SPIN_HOLD_MS,
  interval: SPIN_INTERVAL_MS,
  step(dir, isRepeat) {
    if (!isRepeat) return; // 第一下交給內建的 click
    const api = carouselRef.value?.emblaApi;
    if (dir === "prev") api?.scrollPrev();
    else if (dir === "next") api?.scrollNext();
  },
  finish(dir, repeated) {
    swallowClick = repeated;
  },
});

const arrowListeners = new AbortController();
let arrowsReady = false;
function attachArrowPress() {
  for (const dir of ["prev", "next"]) {
    const el = sectionEl.value?.querySelector(`[data-slot="${dir}"]`);
    if (!el) continue;
    const opt = { signal: arrowListeners.signal };
    el.title = "長按可快轉";
    el.addEventListener(
      "pointerdown",
      (e) => {
        if (e.pointerType === "mouse" && e.button !== 0) return;
        // 10/24：不讓箭頭拿到焦點，改成整個輪播拿焦點（檔頭第 12 點）。click 不受影響，照樣會換張
        e.preventDefault();
        carouselRoot()?.focus({ preventScroll: true });
        el.setPointerCapture?.(e.pointerId);
        arrowPress.start(dir);
      },
      opt,
    );
    for (const type of ["pointerup", "pointercancel", "lostpointercapture"]) {
      el.addEventListener(type, () => arrowPress.stop(), opt);
    }
    // capture：比內建的 @click 先收到，才擋得住它
    el.addEventListener(
      "click",
      (e) => {
        if (!swallowClick) return;
        swallowClick = false;
        e.stopImmediatePropagation();
      },
      { ...opt, capture: true },
    );
    arrowsReady = true;
  }
  // 10/24：按住鍵盤 ← → 時，對應的箭頭亮起來（檔頭第 12 點）。換張本身是 UCarousel 內建的，這裡只負責亮
  const root = carouselRoot();
  if (!root) return;
  const opt = { signal: arrowListeners.signal };
  root.addEventListener(
    "keydown",
    (e) => {
      const dir = KEY_DIR[e.key];
      if (dir && !e.altKey && !e.ctrlKey && !e.metaKey && !e.shiftKey) keyDir.value = dir;
    },
    opt,
  );
  root.addEventListener(
    "keyup",
    (e) => {
      if (KEY_DIR[e.key] === keyDir.value) keyDir.value = null;
    },
    opt,
  );
  // 焦點離開輪播、切到別的視窗時可能收不到 keyup，一律當成放開
  root.addEventListener("focusout", () => (keyDir.value = null), opt);
  window.addEventListener("blur", () => (keyDir.value = null), opt);
}

// ── 10/24：箭頭亮起來（檔頭第 12 點）──
// UCarousel 的外框（role="region" 那一層）：可以拿焦點、← → 由它處理
function carouselRoot() {
  return sectionEl.value?.querySelector('[aria-roledescription="carousel"]');
}
const KEY_DIR = { ArrowLeft: "prev", ArrowRight: "next" };
const keyDir = ref(null); // 鍵盤正按著哪個方向
// 亮哪一顆：鍵盤按著的方向優先，其次是滑鼠、手指按住的箭頭。用 data-pressed 標記，樣式寫在 carouselUi
watch([keyDir, () => arrowPress.activeDir.value], ([key, pointer]) => {
  const lit = key ?? pointer;
  for (const dir of ["prev", "next"]) {
    sectionEl.value
      ?.querySelector(`[data-slot="${dir}"]`)
      ?.toggleAttribute("data-pressed", dir === lit);
  }
});
// 卡片載入完、輪播畫出來之後才找得到箭頭
watch(cards, async (list) => {
  if (arrowsReady || list.length === 0) return;
  await nextTick();
  attachArrowPress();
});
onBeforeUnmount(() => arrowListeners.abort());

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
  // 10/24：data-pressed＝正在按（鍵盤 ← → 或按住箭頭），淡淡的品牌藍底，跟評論專區換頁按鈕按住時一樣（檔頭第 12 點）
  // 底色要不透明：箭頭壓在卡片區（白）和版面留白（灰）的交界上，半透明的藍會透出兩種底色，看起來一半一半。
  // 所以用 color-mix 把 10% 的品牌藍混進白色，算出一個實心的淡藍
  prev: "-start-5 sm:-start-5 rounded text-neutral-text-primary data-pressed:bg-[color-mix(in_srgb,var(--color-brand-primary)_10%,var(--color-neutral-surface))] data-pressed:text-brand-primary",
  next: "-end-5 sm:-end-5 rounded text-neutral-text-primary data-pressed:bg-[color-mix(in_srgb,var(--color-brand-primary)_10%,var(--color-neutral-surface))] data-pressed:text-brand-primary",
};
</script>

<template>
  <!-- 檔頭第 10 點：挑不到評價就整個區塊不顯示 -->
  <!-- 10/23：加 ref，找箭頭用（檔頭第 11 點） -->
  <section v-if="isLoading || errorMessage || cards.length > 0" ref="sectionEl">
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
