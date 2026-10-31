<!--
  MyReviewsView.vue — 會員中心「我的評論」（昱，10/10）
  路由：/member/reviews（?tab=pending 待評論、?tab=reviewed 已評論；沒給是待評論）

  運動中心改成只限會員之後，這一頁是寫評論的唯一入口：
    待評論：本人的、還沒評、還沒過期的現場和預約憑證（規格 2-9）。按「立刻評論」去撰寫頁。
    已評論：本人寫過的評論摘要（規格 2-10）。點一列展開，內容跟單筆頁一樣（MyReviewDetail）。

  ── 為什麼「已評論」展開時才讀完整內容 ─────────────────────────
  清單 API 只回摘要。展開時 MyReviewDetail 才讀完整內容，並在「真的看到館方回覆」時記錄已讀。
  如果清單一打開就讀全部，每一則有回覆的評論都會被當成「顧客看過了」，營運分析的已讀率就失真了。
  內容在「第一次展開」時才建立，所以沒展開的那幾則不會發出任何請求。

  ── 分頁記在網址上（?tab=）──────────────────────────────────
  寫完評論、從單筆頁按「回我的評論」，會回到「已評論」分頁；重新整理也停在同一個分頁。
  用 replace 換分頁：切分頁不該在瀏覽紀錄裡多一筆，否則按上一頁要按好幾次才離得開。

  ── 10/12 外觀改版（參考 Reddit／TikTok 的留言串、Airbnb 的評論列表）──────────
  1. 一張白底面板、一列一則（分隔線），取代原本「一則一張卡片」：同一畫面看得到更多則，列高也壓低了。
  2. 每列左邊一個有底色的圖示方塊：藍色＝現場評論、橘色＝預約評論、紅色＝已下架、黃色＝快到期，掃一眼就分得出來。
  3. 展開後，那一列加上藍色左邊線、展開區用淡藍底，看得出「哪裡到哪裡是這一則的詳情」。
     10/14：淡藍底從 5% 加深到 10%，原本太淡，白色的館方回覆泡泡也襯不出來。
  4. 展開區不再重複列上已經有的資訊（星等、日期、場地時段），只放完整內容、館方回覆、表態與公開切換
    （MyReviewDetail 的 compact 模式）。展開時列上的摘要那一行會藏起來，換成完整內容。
  5. 標籤只標「例外」：公開是預設，不再每列都標「公開中」，只有不公開才標；館方已回覆改成一行小字。

  ── 為什麼沒用 components/list/ListTable.vue ──────────────────
  ListTable 是「寬螢幕表格、窄螢幕卡片」的列表。這一頁不管多寬都是卡片，而且已評論要能展開，
  用表格反而要把展開的內容塞進表格列裡。標籤則用了同一組的 StatusBadge。

  ── 10/25：分頁上的數字要提醒人 ─────────────────────────────
  1. 待評論有快到期的：數字的底色變成橘色（品牌的 action 色）；沒有快到期的是白底。
     不加鬧鐘：進來預設就是待評論，下面清單的快到期那幾列已經有鬧鐘了，分頁上再放一個是重複的。
  2. 已評論有館方新回覆（顧客還沒看過）：不顯示數字，只顯示藍色的「新」；新回覆都看過之後，恢復成白底的數字。
  4. 分頁文字加大到 16px，下面的分隔線加粗到 2px。
  5. 會員中心左邊選單的「我的評論」也有提醒（components/review/MemberReviewsMarker.vue），規則寫在那個檔。
  3. 已評論清單裡，有新回覆的那幾則排到最上面（其他照原本的順序，新的在前）。
     只在讀進來的時候排一次：展開看過之後，那一則不會馬上跳回原位，免得畫面在眼前跳動；下次進來才回到原位。

  ── 10/31：明細視窗、雙欄、鍵盤操作 ─────────────────────────
  1. 點圖示或標題，打開那一筆的明細：現場評論 → 預約明細、預約評論 → 訂單明細（composables/useReviewDetailModals.js）。
  2. 待評論改成雙欄（寬螢幕一列兩則），每則一張卡片。
  3. 已評論的展開狀態由頁面自己管（openKeys），不交給 UCollapsible 的觸發區：
     圖示、標題要能另外點開明細，整列就不能再是同一顆按鈕（按鈕裡面不能放按鈕）。
     展開、收合後焦點放在「這一整則」（li），藍色焦點框剛好框住淡藍底；這時按 Enter 就是收合／展開，↑↓ 換上一則／下一則。
     展開：這一則的頂端貼齊頁首下緣；收合：上一則的頂端貼齊頁首下緣（第一則就用自己）。
  4. 網址帶 ?open=種類-訂單Id（查看頁的「查看其他評論」）：讀完已評論後直接展開那一則、焦點放上去。
  5. 預約評論展開時是淡橘底（跟它的圖示方塊同色），現場評論維持淡藍（REVIEW_KINDS 的 openClass）。
  6. 分頁列最右邊「全部展開／全部收合」：展開 0～1 則時顯示「全部展開」，2 則以上顯示「全部收合」。
  7. 進到待評論，焦點放在第一則的撰寫按鈕；切到已評論，焦點放在第一則（Enter 就展開）。
  8. 顧客回到這個畫面（切換視窗、切換分頁）時安靜地重新讀取，畫面開著時每 30 秒也讀一次：
     館方剛回覆的評論，切回來就看得到。不用 SignalR（還沒做）；演示時員工回覆完、切回前台，回覆馬上出現。
  9. 已評論的順序：館方新回覆 → 已回覆但還沒表態 → 其他（各段裡新的在前）。還沒表態的標「您是否滿意？」（綠字）。
-->
<script setup>
// 10/31：多了 nextTick（?open= 展開之前等清單畫好）、watch、onBeforeUnmount（自動更新）。原本：import { ref, reactive, computed, onMounted } from "vue";
import { ref, reactive, computed, onMounted, onBeforeUnmount, nextTick, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { listPendingReviews, listReviewedReviews } from "@/api/reviewApi";
import { useMemberStore } from "@/stores/member";
import { writeRoute } from "@/router/reviewRoutes";
import { MY_REVIEW_BADGES, TILE_TONES } from "@/constants/review";
import { kindInfo } from "@/utils/review/reviewKinds";
import { hasReviewDraft } from "@/utils/review/draftKey";
import StatusBadge from "@/components/list/StatusBadge.vue";
import StarView from "@/components/review/StarView.vue";
import MyReviewDetail from "@/components/review/MyReviewDetail.vue";
import { useReviewDetailModals } from "@/composables/useReviewDetailModals"; // 10/31：明細視窗

const route = useRoute();
const router = useRouter();
const memberStore = useMemberStore();
const detail = useReviewDetailModals(); // 10/31：點圖示、標題打開預約明細／訂單明細

// ── 分頁 ──
const TAB_VALUES = ["pending", "reviewed"];
const tab = computed({
  // 網址上的值不認得（例如手打錯）就回到「待評論」，不要顯示一個空白的分頁
  get: () => (TAB_VALUES.includes(route.query.tab) ? route.query.tab : "pending"),
  set: (value) => router.replace({ query: { ...route.query, tab: value } }),
});

// ── 兩份清單：各自載入、各自顯示錯誤（一份失敗不影響另一份）──
const pending = ref(null); // null＝還沒載入好
const reviewed = ref(null);
const pendingError = ref("");
const reviewedError = ref("");

async function loadPending() {
  pendingError.value = "";
  try {
    const items = await listPendingReviews();
    // 「有草稿」存在這台電腦的 localStorage，後端不知道，在這裡補上
    const userId = memberStore.member?.userId;
    pending.value = items.map((item) => ({
      ...item,
      // 10/25 改名 ticket → orderId
      hasDraft: hasReviewDraft(userId, item.kind, item.orderId),
    }));
  } catch (e) {
    pendingError.value = e.message;
  }
}

async function loadReviewed() {
  reviewedError.value = "";
  try {
    // 10/25 原本：reviewed.value = await listReviewedReviews();
    // 有館方新回覆的排到最上面（檔頭 10/25 第 3 點）。filter 會保留原本的順序，兩段各自還是新的在前
    const items = await listReviewedReviews();
    // 10/31：中間多一段「已回覆但還沒表態」（檔頭 10/31 第 9 點）。三段互斥、合起來是全部。原本：
    // reviewed.value = [
    //   ...items.filter((item) => item.hasUnviewedReply),
    //   ...items.filter((item) => !item.hasUnviewedReply),
    // ];
    reviewed.value = [
      ...items.filter((item) => item.hasUnviewedReply),
      ...items.filter((item) => !item.hasUnviewedReply && item.awaitingSatisfaction),
      ...items.filter((item) => !item.hasUnviewedReply && !item.awaitingSatisfaction),
    ];
  } catch (e) {
    reviewedError.value = e.message;
  }
}

// 兩份一起載入：分頁上的數字一開始就要對
// 10/31：讀完之後，網址有 ?open= 就展開那一則；沒有就把焦點放在目前分頁的第一則（檔頭 10/31 第 7 點）。
// 原本：onMounted(() => Promise.all([loadPending(), loadReviewed()]));
onMounted(() =>
  Promise.all([loadPending(), loadReviewed()]).then(() =>
    route.query.open ? openFromQuery() : focusFirstOfTab(),
  ),
);

// 10/25：快到期的則數、館方新回覆的則數（檔頭 10/25）
const expiringCount = computed(
  () => pending.value?.filter((item) => item.deadline.expiringSoon).length ?? 0,
);
const newReplyCount = computed(
  () => reviewed.value?.filter((item) => item.hasUnviewedReply).length ?? 0,
);

// 分頁上的數字。還沒載入好時不顯示（undefined），不要先顯示 0 再跳成 3
// 10/25：badge 改成物件時，會蓋掉 UTabs 預設的樣子（灰色外框）。expiring／newReplies 給下面的 #trailing 用
/* 10/25 原本：
const tabs = computed(() => [
  {
    label: "待評論",
    value: "pending",
    icon: "i-lucide-pen-line",
    badge: pending.value?.length || undefined,
  },
  {
    label: "已評論",
    value: "reviewed",
    icon: "i-lucide-message-square-text",
    badge: reviewed.value?.length || undefined,
  },
]);
*/
const tabs = computed(() => [
  {
    label: "待評論",
    value: "pending",
    icon: "i-lucide-pen-line",
    badge: pending.value?.length || undefined,
    expiring: expiringCount.value,
  },
  {
    label: "已評論",
    value: "reviewed",
    icon: "i-lucide-message-square-text",
    badge: reviewed.value?.length || undefined,
    newReplies: newReplyCount.value,
  },
]);

function showPending() {
  tab.value = "pending";
}

// ── 已評論：標籤 ──
// 原本（10/10）：每列都標回覆狀態＋公開與否，四則就有八個標籤，重點反而被淹沒
// function reviewedBadges(item) {
//   if (item.isSpamMarked) return [MY_REVIEW_BADGES.spam];
//   const list = [];
//   if (item.hasUnviewedReply) list.push(MY_REVIEW_BADGES.newReply);
//   else if (item.hasReply) list.push(MY_REVIEW_BADGES.replied);
//   else list.push(MY_REVIEW_BADGES.noReply);
//   list.push(item.isPublic ? MY_REVIEW_BADGES.public : MY_REVIEW_BADGES.private);
//   return list;
// }
// 10/12：標籤只標「例外」——要顧客注意的才標。
//   被下架 → 只標「已下架」（公開、回覆對顧客都不重要了）
//   館方新回覆 → 標（顧客還沒看過）
//   不公開 → 標（公開是預設，公開就不標）
//   「館方已回覆／尚未回覆」不是例外，改成列右邊的一行小字（replyHint）
function reviewedBadges(item) {
  if (item.isSpamMarked) return [MY_REVIEW_BADGES.spam];
  const list = [];
  if (item.hasUnviewedReply) list.push(MY_REVIEW_BADGES.newReply);
  if (!item.isPublic) list.push(MY_REVIEW_BADGES.private);
  return list;
}

// 列右下的回覆狀態小字。三種情況都寫出來，不用「否則」帶過：
//   新回覆已經有標籤了，這裡不重複
function replyHint(item) {
  if (item.isSpamMarked) return null;
  if (item.hasUnviewedReply) return null;
  if (item.hasReply) return { icon: "i-lucide-reply", text: "館方已回覆" };
  if (!item.hasReply) return { icon: "i-lucide-hourglass", text: "尚未回覆" };
  return null;
}

// 列左邊圖示方塊的顏色：例外狀態（已下架、快到期）優先，其次看評論種類
function reviewedTone(item) {
  if (item.isSpamMarked) return { icon: MY_REVIEW_BADGES.spam.icon, cls: TILE_TONES.error };
  return { icon: kindInfo(item.kind).icon, cls: kindInfo(item.kind).tileClass };
}
function pendingTone(item) {
  if (item.deadline.expiringSoon)
    return { icon: MY_REVIEW_BADGES.expiringSoon.icon, cls: TILE_TONES.warning };
  return { icon: kindInfo(item.kind).icon, cls: kindInfo(item.kind).tileClass };
}

// 展開後 MyReviewDetail 改了狀態（記錄已讀、切換公開）→ 清單上的標籤跟著改，不必整份重讀
function onReplyViewed(item) {
  item.hasUnviewedReply = false;
}
function onVisibilityChanged(item, isPublic) {
  item.isPublic = isPublic;
}

// 展開過的那幾筆（第一次展開才建立內容，之後保留）
const opened = reactive(new Set());

// 每一筆的 key：種類＋憑證（同一個 ticket 數字在現場、預約兩邊可能各有一筆）
// 10/25 改名 ticket → orderId
const keyOf = (item) => `${item.kind}-${item.orderId}`;

// ── 10/31：已評論的展開狀態、焦點、捲動、鍵盤（檔頭 10/31 第 3、4 點）──
// 10/30 的版本（收合時置中）還沒 commit，直接換成這一版。
// 等展開／收合的動畫跑完再量，量到的才是最後的位置。
const COLLAPSE_ANIMATION_MS = 300;
// 10/31：展開時，這一則的頂端跟頁首之間多留的距離，大約是一則「沒展開的評論」高度的 1/3～1/2
const EXPAND_GAP_PX = 32;

const openKeys = reactive(new Set()); // 現在展開著的那幾則
const isOpen = (item) => openKeys.has(keyOf(item));
const rowOf = (item) => document.querySelector(`[data-review-key="${keyOf(item)}"]`);

function setOpen(item, open) {
  const key = keyOf(item);
  if (open) {
    openKeys.add(key);
    opened.add(key); // 原本的行為：第一次展開才建立內容
  }
  if (!open) openKeys.delete(key);

  const row = rowOf(item);
  if (!row) return;
  row.focus({ preventScroll: true }); // 焦點放在這一整則，Enter 就是收合／展開
  // 10/31：第一則另外處理——展開時不捲（除非它的底部超出畫面），收合時回到頁面最上方。原本：
  // setTimeout(() => {
  //   if (open) scrollBelowHeader(row, EXPAND_GAP_PX);
  //   if (!open) scrollBelowHeader(row.previousElementSibling ?? row);
  // }, COLLAPSE_ANIMATION_MS);
  const isFirst = row.previousElementSibling === null;
  setTimeout(() => {
    const overflows = row.getBoundingClientRect().bottom > window.innerHeight;
    if (open && isFirst && overflows) scrollBelowHeader(row, EXPAND_GAP_PX);
    if (open && isFirst && !overflows) return; // 第一則整則都看得到：不捲
    if (open && !isFirst) scrollBelowHeader(row, EXPAND_GAP_PX);
    if (!open && isFirst) window.scrollTo({ top: 0, behavior: "smooth" });
    if (!open && !isFirst) scrollBelowHeader(row.previousElementSibling); // 收合：上一則貼齊頁首
  }, COLLAPSE_ANIMATION_MS);
}

function toggle(item) {
  setOpen(item, !isOpen(item));
}

// 只在焦點停在「這一則本身」或列上的展開按鈕時處理：展開區裡的按鈕、開關有自己的鍵盤操作，不搶
function onRowKeydown(event, item) {
  const row = event.currentTarget;
  const onRow = event.target === row || event.target.closest("[data-row-toggle]") !== null;
  if (!onRow) return;

  // 展開按鈕上按 Enter，瀏覽器本來就會觸發 click，這裡只處理焦點在 li 本身的情況
  if ((event.key === "Enter" || event.key === " ") && event.target === row) {
    event.preventDefault();
    toggle(item);
    return;
  }
  // 10/31：這一則展開著、而且有表態選項：→ 跳到第一個選項、← 跳到最後一個（選項之間的 ←→ 由 MyReviewDetail 處理）
  if ((event.key === "ArrowRight" || event.key === "ArrowLeft") && isOpen(item)) {
    const options = [...row.querySelectorAll("[data-satisfaction-options] button:not([disabled])")];
    if (options.length === 0) return;
    event.preventDefault();
    (event.key === "ArrowRight" ? options[0] : options[options.length - 1]).focus();
    return;
  }
  if (event.key === "ArrowDown" || event.key === "ArrowUp") {
    event.preventDefault(); // 不捲動畫面，改成換一則
    const next = event.key === "ArrowDown" ? row.nextElementSibling : row.previousElementSibling;
    if (!next) return;
    next.focus({ preventScroll: true });
    revealRow(next);
  }
}

function headerOffset() {
  const header = document.querySelector("header.sticky");
  return (header ? header.offsetHeight : 0) + 8;
}

// 10/31：多了 extra（再往下多留幾 px）
function scrollBelowHeader(el, extra = 0) {
  const top = el.getBoundingClientRect().top;
  window.scrollTo({ top: window.scrollY + top - headerOffset() - extra, behavior: "smooth" });
}

// ↑↓ 換到的那一則如果在畫面外（或被頁首蓋住），捲到剛好看得到；已經看得到就不動
function revealRow(el) {
  const r = el.getBoundingClientRect();
  if (r.top < headerOffset()) {
    scrollBelowHeader(el);
    return;
  }
  if (r.bottom > window.innerHeight) {
    window.scrollTo({
      top: window.scrollY + r.bottom - window.innerHeight + 8,
      behavior: "smooth",
    });
  }
}

// ── 10/31：全部展開／全部收合（檔頭第 6 點）──
const collapseAllMode = computed(() => openKeys.size >= 2);
// 10/31：按鈕的字級＝分頁標籤的字級（量畫面上第一個分頁標籤）
const tabFontSize = ref(undefined);
onMounted(async () => {
  await nextTick();
  const tabEl = document.querySelector('[role="tab"]');
  if (tabEl) tabFontSize.value = getComputedStyle(tabEl).fontSize;
});
function toggleAll() {
  if (collapseAllMode.value) {
    openKeys.clear();
    return;
  }
  for (const item of reviewed.value ?? []) {
    openKeys.add(keyOf(item));
    opened.add(keyOf(item));
  }
}

// ── 10/31：進到分頁時，焦點放在第一則（檔頭第 7 點）──
// 待評論：第一則的撰寫按鈕；已評論：第一則本身（Enter 就展開）。
// 按鈕的焦點框用 focus:（不是 focus-visible:）：用程式移過去的焦點，瀏覽器常常不畫 focus-visible 的框
async function focusFirstOfTab() {
  await nextTick();
  // 10/31：再等一輪。用滑鼠點分頁時，瀏覽器在 mousedown 處理完之後才把焦點放到分頁上，
  // 太早移過去的話，焦點會被分頁搶回去
  await new Promise((resolve) => setTimeout(resolve, 0));
  if (tab.value === "pending")
    document.querySelector("[data-first-write]")?.focus({ preventScroll: true });
  if (tab.value === "reviewed")
    document.querySelector("[data-review-key]")?.focus({ preventScroll: true });
}
watch(tab, focusFirstOfTab);

// ── 10/31：自動更新（檔頭第 8 點）──
// 已評論：原本畫面上的那幾則留在原位、換成新資料，新出現的放最上面；不重新排序，免得正在看的那一則跳走。
// try/catch 的決定：安靜更新失敗就算了（畫面維持原樣，下次再試），不顯示錯誤，所以在這裡接住。
const REFRESH_MS = 30000;
let refreshing = false;
let refreshTimer = null;

async function refreshQuietly() {
  if (refreshing || document.visibilityState !== "visible") return;
  if (!pending.value || !reviewed.value) return; // 第一次還沒讀完
  refreshing = true;
  try {
    const [freshPending, freshReviewed] = await Promise.all([
      listPendingReviews(),
      listReviewedReviews(),
    ]);
    const userId = memberStore.member?.userId;
    pending.value = freshPending.map((item) => ({
      ...item,
      hasDraft: hasReviewDraft(userId, item.kind, item.orderId),
    }));
    const byKey = new Map(freshReviewed.map((x) => [keyOf(x), x]));
    const known = new Set(reviewed.value.map(keyOf));
    const kept = reviewed.value.filter((x) => byKey.has(keyOf(x))).map((x) => byKey.get(keyOf(x)));
    const added = freshReviewed.filter((x) => !known.has(keyOf(x)));
    reviewed.value = [...added, ...kept];
  } catch {
    // 下次再試
  } finally {
    refreshing = false;
  }
}

function onVisibilityChange() {
  if (document.visibilityState === "visible") refreshQuietly();
}

onMounted(() => {
  window.addEventListener("focus", refreshQuietly);
  document.addEventListener("visibilitychange", onVisibilityChange);
  refreshTimer = setInterval(refreshQuietly, REFRESH_MS);
});
onBeforeUnmount(() => {
  window.removeEventListener("focus", refreshQuietly);
  document.removeEventListener("visibilitychange", onVisibilityChange);
  clearInterval(refreshTimer);
});

// 從查看頁按「查看其他評論」回來（?open=種類-訂單Id）：直接展開那一則
async function openFromQuery() {
  const key = route.query.open;
  if (!key || tab.value !== "reviewed" || !reviewed.value) return;
  const item = reviewed.value.find((x) => keyOf(x) === key);
  if (!item) return;
  await nextTick(); // 清單畫好之後才找得到那一列
  setOpen(item, true);
}
</script>

<template>
  <div>
    <h1 class="mb-6 text-2xl font-bold text-neutral-text-primary">我的評論</h1>

    <!-- content=false：分頁只負責切換，內容放在下面自己排，兩個分頁的載入與錯誤處理才看得清楚 -->
    <!-- 10/25 原本：<UTabs v-model="tab" :items="tabs" :content="false" variant="link" class="mb-4" />
         改成自己畫數字（#trailing）：快到期時數字變橘色，有新回覆時只顯示藍色的「新」（檔頭 10/25）。
         :ui：分頁文字從 14px 加大到 16px，下面的分隔線從 1px 加粗到 2px（藍色的目前分頁線跟著變 2px，才蓋得住） -->
    <UTabs
      v-model="tab"
      :items="tabs"
      :content="false"
      variant="link"
      class="mb-4"
      :ui="{
        trigger: 'text-base',
        list: 'border-b-2',
        indicator: '-bottom-0.5 h-0.5',
      }"
    >
      <template #trailing="{ item }">
        <!-- 已評論、有館方新回覆：只顯示藍色的「新」，不顯示數字；都看過之後恢復成白底的數字 -->
        <template v-if="item.newReplies">
          <UBadge
            label="新"
            color="primary"
            variant="solid"
            class="shrink-0"
            :title="`${item.newReplies} 則有館方新回覆`"
          />
          <span class="sr-only">，{{ item.newReplies }} 則有館方新回覆</span>
        </template>
        <!-- 待評論、有快到期的：數字的底色變橘色；沒有快到期的就是白底 -->
        <template v-else-if="item.badge">
          <UBadge
            :label="item.badge"
            :color="item.expiring ? 'action' : 'neutral'"
            :variant="item.expiring ? 'solid' : 'outline'"
            class="shrink-0"
            :title="item.expiring ? `其中 ${item.expiring} 則快到期` : undefined"
          />
          <span v-if="item.expiring" class="sr-only">，其中 {{ item.expiring }} 則快到期</span>
        </template>
      </template>

      <!-- 10/31：分頁列最右邊「全部展開／全部收合」（只有已評論有展開） -->
      <template #list-trailing>
        <!-- 10/31 調整：
             - 字級跟分頁標籤一樣：直接量分頁標籤的字級套上來（tabFontSize），按鈕跟著等比例縮短
             - 手機只剩箭頭（分頁標籤要完整顯示）；文字只在 sm 以上出現，讀螢幕的人聽 aria-label
             - 「全部展開」灰色（低調）、「全部收合」黑色（顯眼）
             原本：:label="…" class="ms-auto self-center text-base" -->
        <UButton
          v-if="tab === 'reviewed' && reviewed?.length"
          color="neutral"
          variant="ghost"
          size="sm"
          :trailing-icon="collapseAllMode ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'"
          :aria-label="collapseAllMode ? '全部收合' : '全部展開'"
          class="ms-auto self-center"
          :class="collapseAllMode ? 'text-neutral-text-primary' : 'text-neutral-text-secondary'"
          :style="{ fontSize: tabFontSize }"
          @click="toggleAll"
        >
          <span class="hidden sm:inline">{{ collapseAllMode ? "全部收合" : "全部展開" }}</span>
        </UButton>
      </template>
    </UTabs>

    <!-- ════════ 待評論 ════════ -->
    <section v-if="tab === 'pending'" role="tabpanel" aria-label="待評論">
      <UAlert
        v-if="pendingError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        :title="pendingError"
        :actions="[{ label: '重試', color: 'error', variant: 'outline', onClick: loadPending }]"
      />

      <div v-else-if="!pending" class="flex flex-col gap-3" aria-busy="true">
        <USkeleton v-for="n in 3" :key="n" class="h-16 rounded-lg" />
      </div>

      <UEmpty
        v-else-if="pending.length === 0"
        variant="outline"
        icon="i-lucide-message-square-heart"
        title="目前沒有待評論的項目"
        description="到場使用或預約付款之後，可以評論的項目會出現在這裡。"
        class="bg-neutral-surface"
      />

      <!-- 10/12：一張面板、一列一則（原本一則一張卡片，太佔高度） -->
      <!-- 10/31：一列太長，改成雙欄（寬螢幕一列兩則），每則一張卡片。原本：
      <ul
        v-else
        class="divide-y divide-neutral-border overflow-hidden rounded-lg border border-neutral-border bg-neutral-surface"
      >
        <li v-for="item in pending" :key="keyOf(item)" class="flex items-center gap-3 px-4 py-3">
      -->
      <ul v-else class="grid gap-3 md:grid-cols-2">
        <li
          v-for="(item, index) in pending"
          :key="keyOf(item)"
          class="flex items-center gap-3 rounded-lg border border-neutral-border bg-neutral-surface px-4 py-3"
        >
          <!-- 圖示方塊：藍＝現場、橘＝預約、黃＝快到期 -->
          <!-- 10/31：改成按鈕，點了打開明細視窗（現場→預約明細、預約→訂單明細）。原本：
          <span
            class="flex size-10 shrink-0 items-center justify-center rounded-lg"
            :class="pendingTone(item).cls"
            :title="kindInfo(item.kind).label"
          >
            <UIcon :name="pendingTone(item).icon" class="size-5" aria-hidden="true" />
            <span class="sr-only">{{ kindInfo(item.kind).label }}</span>
          </span>
          -->
          <button
            type="button"
            class="flex size-10 shrink-0 cursor-pointer items-center justify-center rounded-lg transition-opacity hover:opacity-80 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
            :class="pendingTone(item).cls"
            :title="detail.label(item.kind)"
            :aria-label="`${kindInfo(item.kind).label}：${detail.label(item.kind)}`"
            @click="detail.open(item.kind, item.orderId)"
          >
            <UIcon :name="pendingTone(item).icon" class="size-5" aria-hidden="true" />
          </button>

          <div class="min-w-0 flex-1">
            <!-- 10/31：標題改成按鈕，點了打開明細視窗（同左邊的圖示）。原本：
            <p class="flex min-w-0 flex-wrap items-baseline gap-x-2">
              <span class="truncate font-semibold text-neutral-text-primary">
                {{ item.context.primary ?? "（查無名稱）" }}
              </span>
              <span v-if="item.context.secondary" class="text-sm text-neutral-text-secondary">
                {{ item.context.secondary }}
              </span>
            </p>
            -->
            <button
              type="button"
              class="flex max-w-full min-w-0 cursor-pointer flex-wrap items-baseline gap-x-2 text-left hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
              :title="detail.label(item.kind)"
              @click="detail.open(item.kind, item.orderId)"
            >
              <span class="truncate font-semibold text-neutral-text-primary">
                {{ item.context.primary ?? "（查無名稱）" }}
              </span>
              <span v-if="item.context.secondary" class="text-sm text-neutral-text-secondary">
                {{ item.context.secondary }}
              </span>
            </button>
            <p class="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
              <!-- 剩餘時間是後端用網站時間（時光機）算好的，跟能不能評的判定一致 -->
              <time
                :datetime="item.deadline.expiresAt.value"
                :title="`評論期限 ${item.deadline.expiresAt.full}`"
                :class="
                  item.deadline.expiringSoon
                    ? 'font-semibold text-semantic-warning'
                    : 'text-neutral-text-secondary'
                "
              >
                <!-- 快到期：左邊的圖示方塊已經換成黃色鬧鐘，這裡只加粗變色；讀螢幕的人另外聽到「快到期」 -->
                <span v-if="item.deadline.expiringSoon" class="sr-only">快到期，</span>
                {{ item.deadline.remainingText }}
              </time>
              <span
                v-if="item.hasDraft"
                class="inline-flex items-center gap-1 text-neutral-text-secondary"
              >
                <UIcon :name="MY_REVIEW_BADGES.hasDraft.icon" aria-hidden="true" />
                {{ MY_REVIEW_BADGES.hasDraft.label }}
              </span>
            </p>
          </div>

          <!-- 主要行動用 action 色。有草稿時改成「繼續撰寫」，顧客才知道進去會看到上次寫的內容 -->
          <!-- 10/25 改名 ticket → orderId -->
          <UButton
            :to="writeRoute(item.kind, String(item.orderId))"
            color="action"
            size="sm"
            icon="i-lucide-pen-line"
            :label="item.hasDraft ? '繼續撰寫' : '立刻評論'"
            class="shrink-0 focus:outline-2 focus:outline-offset-2 focus:outline-brand-action"
            :data-first-write="index === 0 ? '' : undefined"
          />
        </li>
      </ul>
    </section>

    <!-- ════════ 已評論 ════════ -->
    <section v-else role="tabpanel" aria-label="已評論">
      <UAlert
        v-if="reviewedError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        :title="reviewedError"
        :actions="[{ label: '重試', color: 'error', variant: 'outline', onClick: loadReviewed }]"
      />

      <div v-else-if="!reviewed" class="flex flex-col gap-3" aria-busy="true">
        <USkeleton v-for="n in 3" :key="n" class="h-16 rounded-lg" />
      </div>

      <UEmpty
        v-else-if="reviewed.length === 0"
        variant="outline"
        icon="i-lucide-message-square-text"
        title="還沒有寫過評論"
        description="寫好的評論和館方的回覆，都會整理在這裡。"
        :actions="[
          {
            label: '看看待評論',
            color: 'primary',
            variant: 'outline',
            onClick: showPending,
          },
        ]"
        class="bg-neutral-surface"
      />

      <!-- 10/12：一張面板、一列一則；展開的那一列有藍色左邊線，展開區是淡藍底 -->
      <ul
        v-else
        class="divide-y divide-neutral-border overflow-hidden rounded-lg border border-neutral-border bg-neutral-surface"
      >
        <!-- 10/31：展開狀態改由頁面自己管（openKeys），整列不再是一顆按鈕，圖示、標題才能另外點開明細視窗（檔頭 10/31 第 3 點）。
             li 本身可以拿到焦點（tabindex="-1"）：展開、收合後焦點放在這裡，藍框剛好框住淡藍底。
             原本的寫法在這個 li 後面的註解裡（裡面原本的註解符號改成 <!~~ ~~>，才包得進去）。 -->
        <li
          v-for="item in reviewed"
          :key="keyOf(item)"
          :data-review-key="keyOf(item)"
          tabindex="-1"
          class="focus:outline-2 focus:-outline-offset-2 focus:outline-brand-primary"
          @keydown="onRowKeydown($event, item)"
        >
          <!-- 列：展開時藍色左邊線、淡藍底（原本靠 data-state，現在看 isOpen） -->
          <div
            class="flex items-start gap-3 border-l-[3px] px-4 py-3 transition-colors"
            :class="
              isOpen(item)
                ? kindInfo(item.kind).openClass
                : 'border-transparent hover:bg-brand-background'
            "
          >
            <!-- 圖示：點了打開明細視窗（現場→預約明細、預約→訂單明細）。鍵盤也從這顆按鈕打開 -->
            <button
              type="button"
              class="flex size-10 shrink-0 cursor-pointer items-center justify-center rounded-lg transition-opacity hover:opacity-80 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
              :class="reviewedTone(item).cls"
              :title="detail.label(item.kind)"
              :aria-label="`${kindInfo(item.kind).label}：${detail.label(item.kind)}`"
              @click="detail.open(item.kind, item.orderId)"
            >
              <UIcon :name="reviewedTone(item).icon" class="size-5" aria-hidden="true" />
            </button>

            <!-- 其餘部分：點了展開／收合 -->
            <button
              type="button"
              data-row-toggle
              class="flex min-w-0 flex-1 cursor-pointer items-start gap-3 text-left focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
              :aria-expanded="isOpen(item)"
              @click="toggle(item)"
            >
              <span class="block min-w-0 flex-1">
                <span class="flex min-w-0 items-baseline gap-2">
                  <!-- 標題（場地名稱 使用時間／訂單編號 付款方式）：點了打開明細視窗，不展開 -->
                  <span
                    class="truncate font-semibold text-neutral-text-primary hover:underline"
                    :title="detail.label(item.kind)"
                    @click.stop="detail.open(item.kind, item.orderId)"
                  >
                    {{ item.context.primary ?? "（查無名稱）" }}
                  </span>
                  <span
                    v-if="item.context.secondary"
                    class="hidden shrink-0 text-sm text-neutral-text-secondary hover:underline sm:inline"
                    :title="detail.label(item.kind)"
                    @click.stop="detail.open(item.kind, item.orderId)"
                  >
                    {{ item.context.secondary }}
                  </span>
                  <time
                    class="ml-auto shrink-0 text-xs text-neutral-text-secondary"
                    :datetime="item.createdAt.value"
                    :title="item.createdAt.full"
                  >
                    {{ item.createdAt.ago }}
                  </time>
                </span>

                <span class="mt-1 flex min-w-0 items-center gap-2">
                  <StarView :rating="item.starRating" size-class="text-sm" />
                  <!-- 展開後這一行藏起來：下面會顯示完整內容，不重複 -->
                  <span v-show="!isOpen(item)" class="truncate text-sm text-neutral-text-secondary">
                    {{ item.excerpt ?? "只給了星等" }}
                  </span>
                </span>

                <span
                  v-if="reviewedBadges(item).length || replyHint(item) || item.awaitingSatisfaction"
                  class="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1"
                >
                  <StatusBadge v-for="b in reviewedBadges(item)" :key="b.label" :status="b" />
                  <!-- 展開後藏起來：下面就看得到館方回覆了 -->
                  <span
                    v-if="replyHint(item)"
                    v-show="!isOpen(item)"
                    class="inline-flex items-center gap-1 text-xs text-neutral-text-secondary"
                  >
                    <UIcon :name="replyHint(item).icon" aria-hidden="true" />
                    {{ replyHint(item).text }}
                  </span>
                  <!-- 10/31：已回覆、還沒表態：綠字提醒（展開後下面就有表態按鈕，藏起來） -->
                  <span
                    v-if="item.awaitingSatisfaction"
                    v-show="!isOpen(item)"
                    class="inline-flex items-center gap-1 text-xs font-semibold text-semantic-success"
                  >
                    <UIcon name="i-lucide-smile-plus" aria-hidden="true" />
                    您是否滿意？
                  </span>
                </span>
              </span>

              <UIcon
                name="i-lucide-chevron-down"
                class="mt-2.5 size-5 shrink-0 text-neutral-text-secondary transition-transform"
                :class="{ 'rotate-180': isOpen(item) }"
                aria-hidden="true"
              />
            </button>
          </div>

          <!-- 沒有預設插槽：UCollapsible 只負責展開區（動畫、收起後保留內容），開關由上面的按鈕和鍵盤控制 -->
          <UCollapsible :open="isOpen(item)" :unmount-on-hide="false">
            <template #content>
              <!-- 展開區：跟上面那一列同一條藍色左邊線、同一個淡藍底，看得出這是同一則。
                   左邊縮排對齊列上的文字（圖示方塊 2.5rem＋間距 0.75rem＋列的左內距 1rem） -->
              <!-- 10/31：顏色依種類（預約＝淡橘）。原本 class 裡寫死 border-brand-primary bg-brand-primary/10 -->
              <div
                class="border-l-[3px] pr-4 pb-4 pl-4 sm:pl-[4.25rem]"
                :class="kindInfo(item.kind).openClass"
              >
                <!-- 10/25 改名 ticket → orderId / order-id -->
                <!-- 10/31：key 帶上「有沒有回覆」：自動更新時發現館方剛回覆，就重建這一塊，回覆才會出現 -->
                <MyReviewDetail
                  v-if="opened.has(keyOf(item))"
                  :key="`${keyOf(item)}-${item.hasReply}`"
                  compact
                  :kind="item.kind"
                  :order-id="String(item.orderId)"
                  @reply-viewed="onReplyViewed(item)"
                  @visibility-changed="(isPublic) => onVisibilityChanged(item, isPublic)"
                  @satisfaction-changed="item.awaitingSatisfaction = false"
                />
              </div>
            </template>
          </UCollapsible>
        </li>
        <!-- 10/31 原本（10/25 commit 的版本；註解符號改成 <!~~ ~~>）：
        <li v-for="item in reviewed" :key="keyOf(item)">
          <!~~ 預設收起；第一次展開時才建立 MyReviewDetail（才讀完整內容、才記錄已讀）。
               之後收起來也保留（unmount-on-hide=false）：記錄已讀、切換公開還在送出時就收起，
               元件被拆掉的話通知不到清單，標籤會停在舊的狀態。保留也讓再次展開時不必重讀。 ~~>
          <UCollapsible
            :unmount-on-hide="false"
            class="group/row"
            @update:open="(open) => open && opened.add(keyOf(item))"
          >
            <!~~ 觸發區是整列。裡面不能再放按鈕（例如 TimeText），按鈕裡面放按鈕不合 HTML 規範 ~~>
            <button
              type="button"
              class="group flex w-full cursor-pointer items-start gap-3 border-l-[3px] border-transparent px-4 py-3 text-left transition-colors hover:bg-brand-background focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-brand-primary data-[state=open]:border-brand-primary data-[state=open]:bg-brand-primary/10"
            >
              <span
                class="flex size-10 shrink-0 items-center justify-center rounded-lg"
                :class="reviewedTone(item).cls"
                :title="kindInfo(item.kind).label"
              >
                <UIcon :name="reviewedTone(item).icon" class="size-5" aria-hidden="true" />
                <span class="sr-only">{{ kindInfo(item.kind).label }}</span>
              </span>

              <span class="block min-w-0 flex-1">
                <span class="flex min-w-0 items-baseline gap-2">
                  <span class="truncate font-semibold text-neutral-text-primary">
                    {{ item.context.primary ?? "（查無名稱）" }}
                  </span>
                  <span
                    v-if="item.context.secondary"
                    class="hidden shrink-0 text-sm text-neutral-text-secondary sm:inline"
                  >
                    {{ item.context.secondary }}
                  </span>
                  <time
                    class="ml-auto shrink-0 text-xs text-neutral-text-secondary"
                    :datetime="item.createdAt.value"
                    :title="item.createdAt.full"
                  >
                    {{ item.createdAt.ago }}
                  </time>
                </span>

                <span class="mt-1 flex min-w-0 items-center gap-2">
                  <StarView :rating="item.starRating" size-class="text-sm" />
                  <!~~ 展開後這一行藏起來：下面會顯示完整內容，不重複 ~~>
                  <span
                    class="truncate text-sm text-neutral-text-secondary group-data-[state=open]:hidden"
                  >
                    {{ item.excerpt ?? "只給了星等" }}
                  </span>
                </span>

                <span
                  v-if="reviewedBadges(item).length || replyHint(item)"
                  class="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1"
                >
                  <StatusBadge v-for="b in reviewedBadges(item)" :key="b.label" :status="b" />
                  <!~~ 展開後藏起來：下面就看得到館方回覆了 ~~>
                  <span
                    v-if="replyHint(item)"
                    class="inline-flex items-center gap-1 text-xs text-neutral-text-secondary group-data-[state=open]:hidden"
                  >
                    <UIcon :name="replyHint(item).icon" aria-hidden="true" />
                    {{ replyHint(item).text }}
                  </span>
                </span>
              </span>

              <UIcon
                name="i-lucide-chevron-down"
                class="mt-2.5 size-5 shrink-0 text-neutral-text-secondary transition-transform group-data-[state=open]:rotate-180"
                aria-hidden="true"
              />
            </button>

            <template #content>
              <!~~ 展開區：跟上面那一列同一條藍色左邊線、同一個淡藍底，看得出這是同一則。
                   左邊縮排對齊列上的文字（圖示方塊 2.5rem＋間距 0.75rem＋列的左內距 1rem） ~~>
              <div
                class="border-l-[3px] border-brand-primary bg-brand-primary/10 pr-4 pb-4 pl-4 sm:pl-[4.25rem]"
              >
                <!~~ 10/25 改名 ticket → orderId / order-id ~~>
                <MyReviewDetail
                  v-if="opened.has(keyOf(item))"
                  compact
                  :kind="item.kind"
                  :order-id="String(item.orderId)"
                  @reply-viewed="onReplyViewed(item)"
                  @visibility-changed="(isPublic) => onVisibilityChanged(item, isPublic)"
                />
              </div>
            </template>
          </UCollapsible>
        </li>
        -->
      </ul>
    </section>
  </div>
</template>
