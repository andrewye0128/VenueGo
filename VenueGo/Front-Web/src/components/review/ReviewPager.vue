<!--
  ReviewPager.vue — 評論專區的分頁（昱，10/23）
  用法（ReviewIndexView.vue）：
    <ReviewPager :page="vm.page" :total-pages="vm.totalPages" :marks="vm.pageMarks"
                 :sort="vm.filter.sort" :loading="loading" @go="goPage">
      ……評論卡……
    </ReviewPager>
  只有 1 頁時不顯示任何換頁的東西，只把評論卡原樣放出來。

  ── 三種樣子 ───────────────────────────────────────────────
  1. 「左右換頁區」：能用滑鼠懸停（hover: hover、pointer: fine），而且評論卡左右兩邊的空白都至少 64px。
     左右兩邊整塊空白都可以點；平常很低調，滑鼠移上去才亮起來，箭頭會往該方向輕推。
     右下角有一個只顯示、不能點的頁碼。
  2. 「換頁 Bar」：其他所有情況（手機、平板、窄的電腦螢幕）。固定在畫面最下方，中間是 ‹ 3 / 9 ›；
     最左邊可以收合，收合後只留一顆展開按鈕。
  3. 「換頁膠囊」（10/23 晚）：手機橫向（觸控、橫向、畫面高度 500px 以下）。橫向時高度最珍貴，整條 Bar 太擋視線，
     改成右下角的小膠囊 ‹ 3 / 9 ›。收合鈕在膠囊最左邊（跟 Bar 一樣），按了往右收進畫面邊緣，只留一個小把手。
     平板橫向至少 744px 高，不算在內。收合狀態跟 Bar 共用同一份：直向收合了，轉成橫向也是收合的。
  頁碼都只能看、不能打字跳頁（10/23 決定）。

  ── 換頁的方式 ─────────────────────────────────────────────
  - 點一下：換一頁。鍵盤 ← →（輸入框、下拉選單、對話框裡不攔截）。
  - 長按（滑鼠、手指、方向鍵都算）：按下先換一頁，按住 0.3 秒出現快轉遮罩，之後每 0.5 秒換一頁（composables/useRepeatPress.js）。
    長按期間只有頁碼在動，放開才真的載入那一頁，網址也只記最後一頁（上一頁不會一頁一頁退回去）。
    長按期間評論卡上蓋一層半透明遮罩，顯示「正在經過的那一頁」第一則的星等與日期，看得出大概翻到哪裡。
    資料是後端隨列表一起給的 pageMarks（每頁第一則），所以快轉時不用另外發請求。
  - 10/24：放開之後遮罩不會馬上消失，要等新的一頁在遮罩後面畫好，才淡出（0.3 秒）。
    原本放開就拿掉，會先露出變淡的舊頁面、再跳成新頁面，看起來閃一下。
    點一下換頁的「變淡」寫在 ReviewIndexView（淡出、淡入都有動畫）。
  - 10/24 下午：放開的同時，遮罩上的文字下方畫出一個綠色的「圓圈打勾」（約 0.55 秒）。
    告訴使用者「收到了，正在換」，等勾畫完，新的一頁通常也好了，不會覺得放開之後卡住。
    遮罩要等「勾畫完」和「新的一頁畫好」兩件事都做到才淡出：本機載入很快，勾還沒畫完就淡掉會看不到。

  ── 10/24：讓 Bar、膠囊少佔一點畫面 ─────────────────────────
  - 平板、電腦（640px 以上）的 Bar 高度從約 57px 降到約 49px（0.85 倍）：按鈕 48 → 44px，上下留白各 4 → 2px。
    44px 是 Apple 建議的最小觸控尺寸，再小就不好按。手機直向不算擋視線，維持原本的高度。
    平板、電腦的收合鈕跟換頁按鈕一樣寬（112px）。
  - 膠囊看起來縮成約 0.9 倍（按鈕 44 → 38px），但「可以按的範圍」用 ::after 往外撐回 44px，好不好按跟原本一樣。

  ── 換頁 Bar 的收合狀態（10/23）────────────────────────────
  會員：存在 localStorage，跟「這次登入的 token 到期時間」綁在一起。
        進評論專區時比對一次：token 換了（重新登入、換帳號）或過期了，就刪掉舊紀錄、回到展開。
        一次登入期間只要收一次，關掉分頁、另開分頁都一樣。
  訪客：存在 sessionStorage，關掉分頁就回到展開，每個分頁各自記。
  在這一頁按登出時（NavBar），會員資料被清空，這裡馬上改用訪客的狀態。
  localStorage／sessionStorage 被瀏覽器封鎖時會丟例外：一律當成「沒有紀錄」，不讓頁面壞掉。
-->
<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useMemberStore } from "@/stores/member";
import { useRepeatPress, pressHandlers } from "@/composables/useRepeatPress";
import IconChevronLeftBold from "@/components/icons/IconChevronLeftBold.vue";
import IconChevronRightBold from "@/components/icons/IconChevronRightBold.vue";

const props = defineProps({
  page: { type: Number, required: true },
  totalPages: { type: Number, required: true },
  // 每一頁第一則的 { star, date }（date 是 "2026-10-19"），第 n 頁是 marks[n - 1]
  marks: { type: Array, default: () => [] },
  // 目前的排序：newest／highest／lowest，決定遮罩上顯示什麼
  sort: { type: String, required: true },
  loading: { type: Boolean, default: false },
});
const emit = defineEmits(["go"]);

const HINT = "長按可快轉";
/** 左右空白至少要這麼寬，才用「左右換頁區」；比這窄就改用換頁 Bar */
const MIN_ZONE_WIDTH = 64;

const hasPages = computed(() => props.totalPages > 1);

// ── 長按快轉 ──
// previewPage：長按期間「頁碼走到哪」，還沒載入；放開才 emit("go")。null＝沒有在快轉。
const previewPage = ref(null);
const shownPage = computed(() => previewPage.value ?? props.page);

const press = useRepeatPress({
  step(dir) {
    const next = shownPage.value + (dir === "next" ? 1 : -1);
    if (next < 1 || next > props.totalPages) return; // 到頭了：停在原地，不繞回去
    previewPage.value = next;
  },
  finish() {
    const overlayWasShown = overlayShown;
    overlayShown = false;
    if (previewPage.value !== null && previewPage.value !== props.page) {
      if (overlayWasShown) startLanding(); // 遮罩繼續蓋著，等新的一頁畫好（檔頭）
      emit("go", previewPage.value);
    } else previewPage.value = null;
  },
});
// 新的一頁載入完（或載入失敗）就不再需要預覽的頁碼
watch(
  () => [props.page, props.loading],
  ([, loading]) => {
    if (!loading && press.activeDir.value === null) previewPage.value = null;
  },
);

const fastForwarding = computed(() => press.repeating.value && previewPage.value !== null);

// ── 10/24：放開之後，遮罩蓋到新的一頁畫好才拿掉（檔頭）──
// 放開時 useRepeatPress 先把 repeating 改回 false 才呼叫 finish，那時 fastForwarding 已經是 false 了，
// 所以另外記「這次按住期間遮罩有沒有出現過」（flush: "sync"：一變成 true 就記下來，不等畫面更新）。
let overlayShown = false;
watch(
  fastForwarding,
  (v) => {
    if (v) overlayShown = true;
  },
  { flush: "sync" },
);
const landing = ref(false);
/** 打勾動畫的長度：圓圈 0.35 秒，接著勾 0.25 秒（從 0.3 秒開始，跟圓圈重疊一點比較順） */
const CHECK_MS = 550;
let landingTimer = null;
let checkTimer = null;
let checkDone = false; // 勾畫完了
let pageReady = false; // 新的一頁畫好了
function startLanding() {
  landing.value = true;
  checkDone = false;
  pageReady = false;
  clearTimeout(checkTimer);
  checkTimer = setTimeout(() => {
    checkDone = true;
    if (pageReady) endLanding();
  }, CHECK_MS);
  clearTimeout(landingTimer);
  // 保險：萬一一直沒有載入完（例如網址沒有變、沒有發出請求），10 秒後還是拿掉，不要永遠蓋著
  landingTimer = setTimeout(endLanding, 10000);
}
function endLanding() {
  landing.value = false;
  clearTimeout(landingTimer);
  clearTimeout(checkTimer);
}
// 載入結束：flush: "post" 時新的一頁已經放進畫面，再等一個畫格確定畫出來了，才算「新的一頁畫好」
watch(
  () => props.loading,
  (loading) => {
    if (loading || !landing.value) return;
    requestAnimationFrame(() => {
      pageReady = true;
      if (checkDone) endLanding();
    });
  },
  { flush: "post" },
);
onBeforeUnmount(() => {
  clearTimeout(landingTimer);
  clearTimeout(checkTimer);
});
const showOverlay = computed(() => fastForwarding.value || landing.value);
const canPrev = computed(() => shownPage.value > 1);
const canNext = computed(() => shownPage.value < props.totalPages);

// 遮罩上的「正在經過哪裡」：最新 → 日期；評分最高／最低 → 星等＋日期
function dateText(iso) {
  const [y, m, d] = iso.split("-").map(Number);
  const thisYear = new Date().getFullYear();
  return `${y === thisYear ? "" : `${y}年`}${m}月${d}日`;
}
const markText = computed(() => {
  const mark = props.marks[shownPage.value - 1];
  if (!mark) return "";
  if (props.sort === "newest") return dateText(mark.date);
  if (props.sort === "highest" || props.sort === "lowest")
    return `${mark.star} 星・${dateText(mark.date)}`;
  return `${mark.star} 星・${dateText(mark.date)}`; // 不認得的排序（不該發生）：兩個都給
});

// 滑鼠、手指、鍵盤（Tab 到按鈕按 Enter）都由 pressHandlers 處理，見 composables/useRepeatPress.js
const prevHandlers = pressHandlers(press, "prev");
const nextHandlers = pressHandlers(press, "next");

// ── 鍵盤 ← → ──
// 這些地方的方向鍵有自己的用途，不攔截：輸入框、下拉選單、對話框（例如「閱讀全文」視窗）、分頁標籤列
const KEEP_ARROWS =
  'input, textarea, select, [contenteditable="true"], [role="combobox"], [role="listbox"], [role="menu"], [role="dialog"], [role="tablist"], [role="slider"]';
const KEY_DIR = { ArrowLeft: "prev", ArrowRight: "next" };

function onKeydown(e) {
  const dir = KEY_DIR[e.key];
  if (!dir || !hasPages.value) return;
  if (e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) return;
  if (e.target instanceof Element && e.target.closest(KEEP_ARROWS)) return;
  e.preventDefault(); // 不要讓頁面左右捲動
  if (e.repeat) return; // 按住時瀏覽器送來的重複事件：由 useRepeatPress 自己計時
  press.start(dir);
}
function onKeyup(e) {
  if (KEY_DIR[e.key] === press.activeDir.value) press.stop();
}

// ── 用哪一種樣子 ──
const root = ref(null);
const canHover = ref(false);
const zoneLeft = ref(0); // 評論卡左邊到視窗左緣的距離
const zoneRight = ref(0); // 評論卡右邊到視窗右緣的距離（不含捲軸）
let hoverQuery = null;

function measure() {
  const el = root.value;
  if (!el) return;
  const rect = el.getBoundingClientRect();
  zoneLeft.value = Math.max(0, Math.floor(rect.left));
  zoneRight.value = Math.max(0, Math.floor(document.documentElement.clientWidth - rect.right));
}
function onHoverChange() {
  canHover.value = hoverQuery.matches;
}
const useZones = computed(
  () => canHover.value && Math.min(zoneLeft.value, zoneRight.value) >= MIN_ZONE_WIDTH,
);
// 手機橫向（樣子 3）
const landscapePhone = ref(false);
let landscapeQuery = null;
function onLandscapeChange() {
  if (landscapeQuery) landscapePhone.value = landscapeQuery.matches;
}
const usePill = computed(() => !useZones.value && landscapePhone.value);

onMounted(() => {
  hoverQuery = window.matchMedia("(hover: hover) and (pointer: fine)");
  onHoverChange();
  hoverQuery.addEventListener("change", onHoverChange);
  landscapeQuery = window.matchMedia(
    "(pointer: coarse) and (orientation: landscape) and (max-height: 500px)",
  );
  onLandscapeChange();
  landscapeQuery.addEventListener("change", onLandscapeChange);
  measure();
  window.addEventListener("resize", measure);
  window.addEventListener("keydown", onKeydown);
  window.addEventListener("keyup", onKeyup);
});
onBeforeUnmount(() => {
  hoverQuery?.removeEventListener("change", onHoverChange);
  landscapeQuery?.removeEventListener("change", onLandscapeChange);
  window.removeEventListener("resize", measure);
  window.removeEventListener("keydown", onKeydown);
  window.removeEventListener("keyup", onKeyup);
});

// ── 換頁 Bar 的收合狀態（檔頭說明）──
const STORE_KEY = "venuego.reviewPager.collapsed";
const memberStore = useMemberStore();

// 這次登入的 token 到期時間（JWT 的 exp，秒）。沒登入、格式不對、已經過期都回傳 null。
// 只是讀出到期時間來比對，不拿來判斷身分（身分一律由後端驗證）。
function loginExp() {
  try {
    const token = localStorage.getItem("token");
    if (!token) return null;
    const payload = JSON.parse(atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
    if (typeof payload.exp !== "number") return null;
    return payload.exp * 1000 > Date.now() ? payload.exp : null;
  } catch {
    return null;
  }
}

function readCollapsed() {
  try {
    const exp = loginExp();
    const saved = localStorage.getItem(STORE_KEY);
    if (exp !== null) {
      if (saved === String(exp)) return true;
      if (saved !== null) localStorage.removeItem(STORE_KEY); // 上一次登入留下的
      return false;
    }
    if (saved !== null) localStorage.removeItem(STORE_KEY); // 已經登出或過期了
    return sessionStorage.getItem(STORE_KEY) === "1";
  } catch {
    return false;
  }
}

function writeCollapsed(value) {
  try {
    const exp = loginExp();
    if (exp !== null && value) localStorage.setItem(STORE_KEY, String(exp));
    else if (exp !== null && !value) localStorage.removeItem(STORE_KEY);
    else if (exp === null && value) sessionStorage.setItem(STORE_KEY, "1");
    else if (exp === null && !value) sessionStorage.removeItem(STORE_KEY);
  } catch {
    // 存不了就只在這一頁有效
  }
}

const collapsed = ref(readCollapsed());
function setCollapsed(value) {
  collapsed.value = value;
  writeCollapsed(value);
}
// 在這一頁登出：改用訪客的狀態
watch(
  () => memberStore.member,
  (member) => {
    if (member === null) collapsed.value = readCollapsed();
  },
);
</script>

<template>
  <div ref="root" class="relative">
    <slot />

    <template v-if="hasPages">
      <!-- 快轉中的遮罩：只蓋住評論卡；文字跟著畫面捲動停在中間（sticky）。
           10/24：放開後蓋到新的一頁畫好才淡出（檔頭）。出現時不做淡入，按住 0.3 秒就要馬上看得到 -->
      <Transition leave-active-class="transition-opacity duration-300" leave-to-class="opacity-0">
        <div
          v-if="showOverlay"
          class="absolute inset-0 z-10 rounded-lg bg-neutral-surface/75 backdrop-blur-[1px]"
          aria-hidden="true"
        >
          <div
            class="sticky top-[calc(50vh-3rem)] flex flex-col items-center gap-2 py-8 text-center"
          >
            <div class="text-3xl font-bold text-neutral-text-primary tabular-nums">
              第 {{ shownPage }} / {{ totalPages }} 頁
            </div>
            <div v-if="markText" class="text-lg text-neutral-text-secondary">
              從「{{ markText }}」開始
            </div>
            <!-- 10/24：放開之後畫出綠色的圓圈打勾（檔頭）。pathLength="1"：線的長度一律當成 1，動畫好寫 -->
            <svg
              v-if="landing"
              class="landing-check mt-2 size-12 text-semantic-success"
              viewBox="0 0 52 52"
              fill="none"
              stroke="currentColor"
              stroke-width="3.5"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <circle cx="26" cy="26" r="23" pathLength="1" />
              <path d="M15 27 L23 35 L38 18" pathLength="1" />
            </svg>
          </div>
        </div>
      </Transition>

      <!-- ── 樣子 1：左右換頁區 ── -->
      <template v-if="useZones">
        <button
          v-for="side in ['prev', 'next']"
          :key="side"
          type="button"
          class="pager-zone group absolute inset-y-0 select-none"
          :class="side === 'prev' ? 'right-full' : 'left-full'"
          :style="{ width: (side === 'prev' ? zoneLeft : zoneRight) + 'px' }"
          :aria-label="side === 'prev' ? '上一頁' : '下一頁'"
          :aria-disabled="(side === 'prev' ? !canPrev : !canNext) ? 'true' : undefined"
          :data-pressed="press.activeDir.value === side ? 'true' : undefined"
          :data-side="side"
          v-bind="side === 'prev' ? prevHandlers : nextHandlers"
        >
          <span class="sticky top-[calc(50vh-2rem)] flex flex-col items-center gap-1">
            <component
              :is="side === 'prev' ? IconChevronLeftBold : IconChevronRightBold"
              class="pager-arrow size-8"
              aria-hidden="true"
            />
            <span class="pager-hint text-xs whitespace-nowrap">{{ HINT }}</span>
          </span>
        </button>

        <!-- 右下角頁碼：只顯示 -->
        <div
          class="fixed right-4 bottom-4 z-30 rounded-md border border-neutral-border bg-neutral-surface px-3 py-1.5 text-sm font-semibold text-neutral-text-primary shadow-sm tabular-nums"
          role="status"
          :aria-label="`第 ${shownPage} 頁，共 ${totalPages} 頁`"
        >
          {{ shownPage }} / {{ totalPages }}
        </div>
      </template>

      <!-- ── 樣子 3：手機橫向的換頁膠囊（右下角，往右收合）── -->
      <template v-else-if="usePill">
        <!-- 佔位：最後一張卡捲到最下面時，不會被膠囊蓋住 -->
        <div class="h-14" aria-hidden="true"></div>

        <div
          v-if="!collapsed"
          class="fixed right-[max(0.5rem,env(safe-area-inset-right))] bottom-[max(0.5rem,env(safe-area-inset-bottom))] z-30 flex items-center rounded-full border border-neutral-border bg-neutral-surface py-0.5 pr-1 shadow-md"
        >
          <!-- 收合鈕：最左邊（10/23 從最右邊移過來，跟 Bar 一樣在左邊；也不會緊貼著「下一頁」被誤按），
               跟換頁按鈕之間用一條細線隔開。圖示用「→|」：表示整個膠囊會收進右邊緣 -->
          <!-- 10/24：膠囊看起來縮成約 0.9 倍，按鈕加 pill-hit：可以按的範圍往外撐回 44px（style 裡） -->
          <button
            type="button"
            class="pager-button pill-hit h-9.5 w-9"
            aria-label="收合換頁列"
            @click="setCollapsed(true)"
          >
            <UIcon name="i-lucide-arrow-right-to-line" class="size-4.5" aria-hidden="true" />
          </button>
          <span class="mx-0.5 h-6 w-px bg-neutral-border" aria-hidden="true"></span>
          <button
            type="button"
            class="pager-button pill-hit size-9.5 select-none"
            aria-label="上一頁"
            :aria-disabled="!canPrev ? 'true' : undefined"
            :data-pressed="press.activeDir.value === 'prev' ? 'true' : undefined"
            v-bind="prevHandlers"
          >
            <IconChevronLeftBold class="size-4.5" aria-hidden="true" />
          </button>
          <span
            class="min-w-11 text-center text-sm font-semibold text-neutral-text-primary tabular-nums"
            role="status"
            :aria-label="`第 ${shownPage} 頁，共 ${totalPages} 頁`"
          >
            {{ shownPage }} / {{ totalPages }}
          </span>
          <button
            type="button"
            class="pager-button pill-hit size-9.5 select-none"
            aria-label="下一頁"
            :aria-disabled="!canNext ? 'true' : undefined"
            :data-pressed="press.activeDir.value === 'next' ? 'true' : undefined"
            v-bind="nextHandlers"
          >
            <IconChevronRightBold class="size-4.5" aria-hidden="true" />
          </button>
        </div>

        <!-- 收合後：貼在畫面右邊緣的小把手，看起來 24px 寬，可以按的範圍往左撐到 44px -->
        <button
          v-else
          type="button"
          class="fixed right-0 bottom-[max(0.5rem,env(safe-area-inset-bottom))] z-30 flex h-11 w-[calc(2.75rem+env(safe-area-inset-right))] items-center justify-end"
          aria-label="展開換頁列"
          @click="setCollapsed(false)"
        >
          <span
            class="flex h-11 w-[calc(1.5rem+env(safe-area-inset-right))] items-center justify-center rounded-l-lg border border-r-0 border-neutral-border bg-neutral-surface pr-[env(safe-area-inset-right)] text-neutral-text-primary shadow-md"
          >
            <UIcon name="i-lucide-arrow-left-from-line" class="size-5" aria-hidden="true" />
          </span>
        </button>
      </template>

      <!-- ── 樣子 2：換頁 Bar ── -->
      <template v-else>
        <!-- 佔位：Bar 是固定在畫面底部的，最後一張卡才不會被蓋住 -->
        <div :class="collapsed ? 'h-14' : 'h-20 sm:h-16'" aria-hidden="true"></div>

        <div
          v-if="!collapsed"
          class="fixed inset-x-0 bottom-0 z-30 border-t border-neutral-border bg-neutral-surface px-2 pt-1 pb-[max(0.25rem,env(safe-area-inset-bottom))] sm:pt-0.5 sm:pb-[max(0.125rem,env(safe-area-inset-bottom))]"
        >
          <!-- 10/24：平板以上（640px 起）收合鈕跟換頁按鈕一樣寬（112px），兩側欄位跟著加寬，中間才會置中 -->
          <div
            class="mx-auto grid max-w-7xl grid-cols-[3rem_1fr_3rem] items-center sm:grid-cols-[7rem_1fr_7rem]"
          >
            <button
              type="button"
              class="pager-button h-12 w-12 sm:h-11 sm:w-28"
              aria-label="收合換頁列"
              @click="setCollapsed(true)"
            >
              <UIcon name="i-lucide-chevron-down" class="size-6" aria-hidden="true" />
            </button>

            <div class="flex items-center justify-center gap-2">
              <button
                type="button"
                class="pager-button h-12 w-12 select-none sm:h-11 sm:w-28"
                aria-label="上一頁"
                :title="canHover ? HINT : undefined"
                :aria-disabled="!canPrev ? 'true' : undefined"
                :data-pressed="press.activeDir.value === 'prev' ? 'true' : undefined"
                v-bind="prevHandlers"
              >
                <IconChevronLeftBold class="size-6" aria-hidden="true" />
              </button>
              <span
                class="min-w-16 text-center text-base font-semibold text-neutral-text-primary tabular-nums"
                role="status"
                :aria-label="`第 ${shownPage} 頁，共 ${totalPages} 頁`"
              >
                {{ shownPage }} / {{ totalPages }}
              </span>
              <button
                type="button"
                class="pager-button h-12 w-12 select-none sm:h-11 sm:w-28"
                aria-label="下一頁"
                :title="canHover ? HINT : undefined"
                :aria-disabled="!canNext ? 'true' : undefined"
                :data-pressed="press.activeDir.value === 'next' ? 'true' : undefined"
                v-bind="nextHandlers"
              >
                <IconChevronRightBold class="size-6" aria-hidden="true" />
              </button>
            </div>
            <span aria-hidden="true"></span>
          </div>
        </div>

        <!-- 收合後：只留展開按鈕，跟收合按鈕同一個位置，貼齊畫面底部（跟 Bar 一樣）。
             看起來的高度是 Bar 的 2/3（約 36px），可以按的範圍用外層撐到 48px，手指才好按。
             iPhone 底部有橫條（安全區）時，看得到的那塊往下延伸蓋住安全區，按鈕本身還是在安全區上方 -->
        <button
          v-else
          type="button"
          class="fixed bottom-0 left-2 z-30 flex h-[calc(3rem+env(safe-area-inset-bottom))] w-12 items-end justify-center"
          aria-label="展開換頁列"
          @click="setCollapsed(false)"
        >
          <span
            class="flex h-[calc(2.25rem+env(safe-area-inset-bottom))] w-12 items-start justify-center rounded-t-lg border border-b-0 border-neutral-border bg-neutral-surface pt-1.5 text-neutral-text-primary shadow-sm"
          >
            <UIcon name="i-lucide-chevron-up" class="size-6" aria-hidden="true" />
          </span>
        </button>
      </template>
    </template>
  </div>
</template>

<style scoped>
/* ── 左右換頁區 ──
   平常：箭頭很淡、說明文字隱藏，讓注意力留在評論卡。
   滑鼠移上去：底色微亮、箭頭變清楚，並往該方向輕推（動畫）；0.4 秒後才出現「長按可快轉」，不會一移過去就跳字。
   按下去（滑鼠、手指，或按住方向鍵）：底色更亮、箭頭放大一點。三種方式看起來一樣。
   到第一頁／最後一頁時用 aria-disabled，不用 disabled：長按快轉到底時按鈕若真的被停用，
   瀏覽器就不會再把「放開」送給它，快轉會停不下來、也不會載入。 */
.pager-zone {
  display: flex;
  align-items: flex-start; /* 不能撐滿高度，裡面的箭頭才能 sticky 停在畫面中間 */
  justify-content: center;
  cursor: pointer;
  color: var(--color-brand-primary);
  border-radius: 0.5rem;
  -webkit-touch-callout: none;
  touch-action: manipulation;
  transition: background-color 0.2s;
}
.pager-zone[aria-disabled="true"] {
  cursor: default;
}
.pager-zone[aria-disabled="true"] .pager-arrow,
.pager-zone[aria-disabled="true"] .pager-hint {
  visibility: hidden;
}
.pager-arrow {
  opacity: 0.25;
  transition:
    opacity 0.2s,
    scale 0.15s;
}
.pager-hint {
  opacity: 0;
  color: var(--color-neutral-text-secondary);
  transition: opacity 0.2s;
}
.pager-zone:not([aria-disabled="true"]):hover {
  background-color: color-mix(in srgb, var(--color-brand-primary) 5%, transparent);
}
.pager-zone:not([aria-disabled="true"]):hover .pager-arrow {
  opacity: 1;
}
.pager-zone:not([aria-disabled="true"]):hover .pager-hint {
  opacity: 1;
  transition-delay: 0.4s;
}
.pager-zone[data-side="prev"]:not([aria-disabled="true"]):hover .pager-arrow {
  animation: nudge-left 0.9s ease-in-out infinite;
}
.pager-zone[data-side="next"]:not([aria-disabled="true"]):hover .pager-arrow {
  animation: nudge-right 0.9s ease-in-out infinite;
}
.pager-zone[data-pressed="true"]:not([aria-disabled="true"]) {
  background-color: color-mix(in srgb, var(--color-brand-primary) 12%, transparent);
}
.pager-zone[data-pressed="true"]:not([aria-disabled="true"]) .pager-arrow {
  opacity: 1;
  scale: 1.15;
  animation: none;
}
.pager-zone:focus-visible {
  outline: 2px solid var(--color-brand-primary);
  outline-offset: -2px;
}
@keyframes nudge-left {
  0%,
  100% {
    translate: 0 0;
  }
  50% {
    translate: -6px 0;
  }
}
@keyframes nudge-right {
  0%,
  100% {
    translate: 0 0;
  }
  50% {
    translate: 6px 0;
  }
}

/* ── 換頁 Bar 的按鈕 ── */
.pager-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 0.5rem;
  color: var(--color-neutral-text-primary);
  cursor: pointer;
  -webkit-touch-callout: none;
  touch-action: manipulation;
  transition:
    background-color 0.15s,
    scale 0.15s;
}
.pager-button[aria-disabled="true"] {
  cursor: default;
  opacity: 0.3;
}
@media (hover: hover) {
  .pager-button:not([aria-disabled="true"]):hover {
    background-color: color-mix(in srgb, var(--color-brand-primary) 8%, transparent);
  }
}
.pager-button[data-pressed="true"]:not([aria-disabled="true"]) {
  background-color: color-mix(in srgb, var(--color-brand-primary) 16%, transparent);
  color: var(--color-brand-primary);
  scale: 1.08;
}
.pager-button:focus-visible {
  outline: 2px solid var(--color-brand-primary);
  outline-offset: 2px;
}
/* 10/24：膠囊的按鈕看起來只有 38px 高，可以按的範圍往外撐到 44px（Apple 建議的最小觸控尺寸）。
   透明的 ::after 比按鈕大一圈，點在上面就等於點到按鈕。相鄰按鈕撐出去的部分會重疊，重疊處由後面的按鈕接手。 */
.pill-hit {
  position: relative;
}
.pill-hit::after {
  content: "";
  position: absolute;
  inset: -3px -4px;
}

/* ── 10/24：快轉放開後的圓圈打勾 ──
   線的長度是 1（pathLength），先用虛線把整條線藏起來（dashoffset 1），再動畫到 0，看起來就像一筆畫出來 */
.landing-check circle,
.landing-check path {
  stroke-dasharray: 1;
  stroke-dashoffset: 1;
}
.landing-check circle {
  animation: draw-line 0.35s ease-out forwards;
}
.landing-check path {
  animation: draw-line 0.25s ease-out 0.3s forwards;
}
@keyframes draw-line {
  to {
    stroke-dashoffset: 0;
  }
}

/* 不想看動畫的人（系統設定「減少動態效果」）：不推箭頭；打勾直接顯示完整的 */
@media (prefers-reduced-motion: reduce) {
  .pager-zone .pager-arrow {
    animation: none !important;
  }
  .landing-check circle,
  .landing-check path {
    animation: none;
    stroke-dashoffset: 0;
  }
}
</style>
