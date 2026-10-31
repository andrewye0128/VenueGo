<!--
    MyReviewView.vue — 我的評論（取代 Views/CReview/ShowMyReviewPage.cshtml）

    ── 10/10：內容抽到 components/review/MyReviewDetail.vue ─────────────
    會員中心「我的評論 › 已評論」展開後也要顯示同樣的內容，所以把評論卡片、館方回覆、表態、
    公開切換、讀取與記錄已讀的邏輯整段搬到 MyReviewDetail（程式碼沒改）。
    這個檔案只剩外框：標題、預覽提示、返回按鈕。下面的說明仍然適用，只是程式碼在 MyReviewDetail 裡。
    返回按鈕：顧客改回「我的評論」（會員中心現在是唯一的入口）；員工預覽維持「返回評論專區」。

    路由：/reviews/visit/:id           → kind='visit'（id ＝ 訂單 Id；10/15 以前是現場評論憑證 Id）
          /reviews/booking/:orderId    → kind='booking'
          /reviews/preview/:reviewId   → previewId（員工預覽，從館方清單開過來）

    ── 員工預覽（2026-09-26 取代原本的 ?preview=1）──────────────
    跟顧客用同一個畫面，員工看到的就是顧客看到的（包括「館方回覆了你的評論」提示）。
    差別只有三個：
      1. 資料從員工專用的 API 拿（規格 2-8）。顧客的評論要會員本人登入才打得開，
         員工用這支才看得到。
      2. 所有會改資料的按鈕都停用（公開開關、滿意度）。
      3. 不送「已看過回覆」的紀錄，不會汙染營運分析的已讀率。
    原本的 ?preview=1 拿掉了：它只是一個網址參數，有沒有加全靠自覺。

    ── 「顧客已看過回覆」改成另外一支 POST ─────────────────────
    Razor 版是「打開頁面（GET）就順便寫入 ReplyViewedAt」。改成兩步：
      1. GET 只讀資料
      2. 頁面顯示出來之後，若有回覆且還沒記錄過，才 POST /reply-viewed
    好處有兩個：
      ・員工預覽時不送第 2 步，就不會汙染營運分析的已讀率（交接文件第三節第 8 項）
      ・「館方回覆了你的評論」提示終於看得到了。
        Razor 版是先寫入 ReplyViewedAt 才組 ViewModel，
        所以 HasUnviewedReply 永遠是 false，那個提示從來沒出現過。
        現在 GET 拿到的是寫入之前的狀態，第一次打開時提示會正常出現。
-->
<script setup>
// 10/31：多了 onMounted、nextTick（一進來把焦點放在「回我的…」）。原本：import { computed, ref } from "vue";
import { computed, ref, onMounted, nextTick } from "vue";
import { useRoute } from "vue-router";
import { ErrorCodes } from "@/constants/errorCodes";
import MyReviewDetail from "@/components/review/MyReviewDetail.vue";
import { REVIEW_SOURCES } from "@/constants/review"; // 10/31：底部按鈕回來源
import { moveFocusByArrows } from "@/utils/arrowFocus"; // 10/31：←→ 換按鈕

// 顧客模式傳 kind + ticket；預覽模式只傳 previewId（種類由 API 回傳的 kind 決定）
// 10/25：ticket 改名 orderId（訂單 Id）
const props = defineProps({
  kind: { type: String, default: null },
  // 10/25 改名 ticket → orderId
  orderId: { type: String, default: null },
  previewId: { type: String, default: null },
});

const isPreview = computed(() => props.previewId != null);

// 10/23：讀取失敗的原因（MyReviewDetail 的 load-error）。
// 沒登入的人去不了「我的評論」（會被帶去登入頁），所以改放「登入後查看」和「先看看評論專區」。
const route = useRoute();
const loadErrorCode = ref("");
const needsLogin = computed(() => loadErrorCode.value === ErrorCodes.NotLoggedIn);

// ── 10/31：底部按鈕（一整行，左右各半）──
// 從我的預約／我的票券來（?from=reservations／tickets）：左「← 回我的預約／票券」、右「查看其他評論 →」
// 從撰寫頁送出後過來（?from=write）、或沒帶 from：只放右半邊的「回我的評論 →」
// 右邊那顆回「已評論」，並直接展開這一則（?open=，MyReviewsView 的 openFromQuery）
const backSource = computed(() => {
  const from = route.query.from;
  if (from === "reservations" || from === "tickets") return REVIEW_SOURCES[from];
  if (from === "write" || from == null) return null;
  return null; // 不認得的值（網址被改過）：當成沒有來源
});
// 10/31：一進來焦點放在「回我的…」按鈕（有來源是左邊那顆，沒有就是右邊的「回我的評論」）。
// 焦點框用 focus:（按鈕的 class）：用程式移過去的焦點，瀏覽器常常不畫 focus-visible 的框，要按個鍵才出現
// preventScroll：按鈕在頁面最下面，不要一進來就捲到底
onMounted(async () => {
  await nextTick();
  document.querySelector("[data-initial-focus]")?.focus({ preventScroll: true });
});

// 10/31：焦點在底部按鈕上時，← → 換到另一顆（只有一顆時不動）。寫法搬到 utils/arrowFocus.js，確認視窗也用
const onButtonsKeydown = moveFocusByArrows;

const reviewedLink = computed(() => ({
  name: "member-reviews",
  query: { tab: "reviewed", open: `${props.kind}-${props.orderId}` },
}));
</script>

<template>
  <main class="mx-auto max-w-2xl px-4 py-10 md:px-6">
    <h1 class="mb-6 text-2xl font-bold text-neutral-text-primary">我的評論</h1>

    <UAlert
      v-if="isPreview"
      color="neutral"
      variant="outline"
      icon="i-lucide-eye"
      title="預覽模式：這是顧客看到的畫面"
      description="按鈕都已停用，不會改到任何資料，也不會記錄「顧客已看過回覆」。"
      class="mb-4"
    />

    <!-- 10/25 改名 ticket → orderId / order-id -->
    <MyReviewDetail
      :kind="kind"
      :order-id="orderId"
      :preview-id="previewId"
      @load-error="(code) => (loadErrorCode = code)"
    >
      <template v-if="needsLogin" #error-action>
        <div class="flex flex-wrap justify-center gap-3">
          <UButton
            :to="{ name: 'login', query: { redirect: route.fullPath } }"
            color="primary"
            label="登入後查看"
          />
          <UButton
            :to="{ name: 'review-index' }"
            color="primary"
            variant="outline"
            label="先看看評論專區"
          />
        </div>
      </template>
    </MyReviewDetail>

    <UButton
      v-if="isPreview"
      :to="{ name: 'review-index' }"
      color="primary"
      variant="outline"
      icon="i-lucide-arrow-left"
      label="返回評論專區"
      class="mt-6"
    />
    <!-- 10/23：需要登入時，上面的錯誤框已經有按鈕，這顆不顯示 -->
    <!-- 10/31：改成一整行、左右各半（說明在 script 的 backSource）。原本：
    <UButton
      v-else-if="!needsLogin"
      :to="{ name: 'member-reviews', query: { tab: 'reviewed' } }"
      color="primary"
      variant="outline"
      icon="i-lucide-arrow-left"
      label="回我的評論"
      class="mt-6"
    />
    -->
    <!-- 10/31：左右方向鍵在兩顆按鈕之間換焦點（onButtonsKeydown）。原本沒有 @keydown -->
    <div v-else-if="!needsLogin" class="mt-6 grid grid-cols-2 gap-3" @keydown="onButtonsKeydown">
      <UButton
        v-if="backSource"
        :to="backSource.to"
        color="primary"
        variant="outline"
        size="lg"
        icon="i-lucide-arrow-left"
        :label="backSource.backLabel"
        class="justify-center focus:outline-2 focus:outline-offset-2 focus:outline-brand-primary"
        data-initial-focus
      />
      <UButton
        :to="reviewedLink"
        color="primary"
        variant="outline"
        size="lg"
        trailing-icon="i-lucide-arrow-right"
        :label="backSource ? '查看其他評論' : '回我的評論'"
        class="col-start-2 justify-center focus:outline-2 focus:outline-offset-2 focus:outline-brand-primary"
        :data-initial-focus="backSource ? undefined : ''"
      />
    </div>
  </main>
</template>
