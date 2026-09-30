<!--
    MyReviewView.vue — 我的評論（取代 Views/CReview/ShowMyReviewPage.cshtml）

    路由：/reviews/visit/:token        → kind='visit'
          /reviews/booking/:id         → kind='booking'
          /reviews/preview/:reviewId   → previewId（員工預覽，從館方清單開過來）

    ── 員工預覽（2026-09-26 取代原本的 ?preview=1）──────────────
    跟顧客用同一個畫面，員工看到的就是顧客看到的（包括「館方回覆了你的評論」提示）。
    差別只有三個：
      1. 資料從員工專用的 API 拿（規格 2-8）。員工不需要知道顧客的 QR token，
         預約評論也不會因為「要會員登入」而打不開。
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
import { ref, computed, onMounted } from "vue";
import {
  getMyReview,
  getReviewPreview,
  markReplyViewed,
  setVisibility,
  setSatisfaction,
} from "@/api/reviewApi";
import { ErrorCodes } from "@/constants/errorCodes";
import StarView from "@/components/review/StarView.vue";
import ReviewKindIcon from "@/components/review/ReviewKindIcon.vue";

// 顧客模式傳 kind + ticket；預覽模式只傳 previewId（種類由 API 回傳的 kind 決定）
const props = defineProps({
  kind: { type: String, default: null },
  ticket: { type: String, default: null },
  previewId: { type: String, default: null },
});

const vm = ref(null);
const loadError = ref("");
const actionError = ref("");

// 打開頁面那一刻「是不是第一次看到回覆」。之後就算記錄成已讀，這一次的提示也保留。
const showNewReplyBanner = ref(false);

const isPreview = computed(() => props.previewId != null);

function load() {
  return isPreview.value
    ? getReviewPreview(props.previewId)
    : getMyReview(props.kind, props.ticket);
}

// 滿意度的三個選項。顯示用的翻譯放在前端：它純粹是畫面文字，不影響任何資料。
// 圖示名稱寫完整字串，Nuxt UI 才掃描得到（見 reviewKinds.js 的說明）。
const SATISFACTION = [
  { value: 2, text: "滿意", icon: "i-lucide-smile" },
  { value: 1, text: "普通", icon: "i-lucide-meh" },
  { value: 0, text: "不滿意", icon: "i-lucide-frown" },
];
const satisfactionOf = (v) => SATISFACTION.find((s) => s.value === v);

onMounted(async () => {
  try {
    vm.value = await load();
  } catch (e) {
    loadError.value = e.message;
    return;
  }

  const reply = vm.value.reply;
  // 被下架的評論也照樣記錄已讀（跟 Razor 版一致），只是畫面上顯示的是下架提示
  if (reply && !reply.isViewed) {
    showNewReplyBanner.value = true;
    if (!isPreview.value) {
      try {
        await markReplyViewed(props.kind, props.ticket);
        reply.isViewed = true;
      } catch (e) {
        // 這是營運分析用的紀錄，失敗了不該打擾顧客。後端會有 log；前端留一行 warn 方便開發時看到。
        console.warn("[MyReviewView] 已讀紀錄沒有存到：", e);
      }
    }
  }
});

// ── 公開切換 ──
// 畫面先跟著切（使用者點了就該有反應），失敗再切回來並說明原因。
const savingVisibility = ref(false);
const isPublic = computed({
  get: () => vm.value?.isPublic ?? false,
  set: async (v) => {
    if (isPreview.value) return; // 保險：開關在預覽時本來就是停用的
    const before = vm.value.isPublic;
    vm.value.isPublic = v;
    actionError.value = "";
    savingVisibility.value = true;
    try {
      const data = await setVisibility(props.ticket, v);
      vm.value.isPublic = data.isPublic; // 以後端確認後的值為準
    } catch (e) {
      vm.value.isPublic = before;
      actionError.value = e.message;
    } finally {
      savingVisibility.value = false;
    }
  },
});

// ── 對回覆表態 ──
// 三顆按鈕各自轉圈（全站預設 loadingAuto），轉圈期間其他兩顆也要停用，避免連點兩個不同的答案。
const savingSatisfaction = ref(false);

async function rate(value) {
  if (isPreview.value) return; // 保險：按鈕在預覽時本來就是停用的
  actionError.value = "";
  savingSatisfaction.value = true;
  try {
    const data = await setSatisfaction(props.kind, props.ticket, value);
    vm.value.reply.satisfaction = data.satisfaction;
    vm.value.reply.canRateSatisfaction = false;
  } catch (e) {
    actionError.value = e.message;
    if (e.errorCode === ErrorCodes.AlreadyRated) {
      // 另一個分頁已經表態過了：重新讀一次，畫面才會跟資料庫一致
      vm.value = await load();
    }
  } finally {
    savingSatisfaction.value = false;
  }
}
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

    <div
      v-if="loadError"
      class="rounded-lg border border-neutral-border bg-neutral-surface px-4 py-10 text-center"
    >
      <UIcon
        name="i-lucide-frown"
        class="mb-3 size-10 text-neutral-text-secondary"
        aria-hidden="true"
      />
      <p class="mb-4 text-neutral-text-primary">{{ loadError }}</p>
      <UButton
        :to="{ name: 'review-index' }"
        color="primary"
        variant="outline"
        label="回評論專區"
      />
    </div>

    <p v-else-if="!vm" class="py-10 text-center text-neutral-text-secondary">載入中⋯</p>

    <template v-else>
      <!-- ── 狀態提示：被下架、有新回覆 ──
           下架理由是「顧客版」的說明（ReviewPolicy.SpamReasonInfos 的 CustomerText）：
           只說評論含有什麼，不引用被判定有問題的字句，也不指控顧客。
           顧客看得懂理由，才比較不會懷疑館方在壓負評。 -->
      <UAlert
        v-if="vm.isSpamMarked"
        color="error"
        variant="subtle"
        icon="i-lucide-octagon-alert"
        title="這則評論未通過審核，已下架"
        class="mb-4"
      >
        <template #description>
          <p v-if="vm.spamReasonText">理由：{{ vm.spamReasonText }}</p>
          <p>目前不會顯示在公開評論區，也無法切換公開狀態。如有疑問，歡迎洽詢服務櫃台。</p>
        </template>
      </UAlert>
      <UAlert
        v-else-if="showNewReplyBanner"
        color="primary"
        variant="subtle"
        icon="i-lucide-message-circle-more"
        title="館方回覆了你的評論"
        class="mb-4"
      />

      <!-- ── 評論卡片 ── -->
      <article class="overflow-hidden rounded-lg border border-neutral-border bg-neutral-surface">
        <div class="p-4 md:p-6">
          <div class="mb-2 flex flex-wrap items-start justify-between gap-2">
            <div class="flex flex-wrap items-center gap-2">
              <StarView :rating="vm.starRating" size-class="text-xl" />
              <span class="font-semibold text-neutral-text-primary">{{ vm.displayName }}</span>
              <UBadge
                v-if="vm.isAnonymous"
                color="neutral"
                variant="outline"
                icon="i-mdi-incognito"
                label="匿名"
              />
            </div>
            <time class="text-sm text-neutral-text-secondary" :datetime="vm.createdAt.value">
              {{ vm.createdAt.full }}
            </time>
          </div>

          <p v-if="vm.isRatingOnly" class="mb-3 text-neutral-text-secondary italic">
            （只給了星等，沒有留下文字）
          </p>
          <template v-else>
            <!-- v-text：理由見 ReviewCard.vue -->
            <p
              class="mb-1 whitespace-pre-wrap wrap-anywhere text-neutral-text-primary"
              v-text="vm.content"
            ></p>
            <!-- 作者看到的是公開版本。有東西被遮蔽時說一聲，作者才不會以為系統吃掉了字 -->
            <p
              v-if="vm.contentMasked && !vm.isSpamMarked"
              class="mb-3 flex gap-1 text-sm text-neutral-text-secondary"
            >
              <UIcon name="i-lucide-shield-check" class="mt-0.5 shrink-0" aria-hidden="true" />
              公開顯示時，電話、Email
              等個人資料、外部連結與不雅字詞會自動隱藏。你看到的就是其他人看到的樣子。
            </p>
            <div v-else class="mb-2"></div>
          </template>

          <div v-if="vm.mentionsVenue || vm.mentionsStaff" class="mb-3 flex flex-wrap gap-1">
            <UBadge
              v-if="vm.mentionsVenue"
              color="neutral"
              variant="outline"
              icon="i-lucide-hash"
              label="場地設施"
            />
            <UBadge
              v-if="vm.mentionsStaff"
              color="neutral"
              variant="outline"
              icon="i-lucide-hash"
              label="人員服務"
            />
          </div>

          <!-- 系統補的資訊：現場評論是場地與時段，預約評論是訂單編號 -->
          <div
            v-if="vm.context.primary"
            class="flex flex-wrap items-center gap-x-2 gap-y-1 border-t border-neutral-border pt-2 text-sm text-neutral-text-secondary"
          >
            <span class="inline-flex items-center gap-1">
              <!-- 種類用 API 回傳的（預覽模式下網址上沒有種類） -->
              <ReviewKindIcon :kind="vm.kind" />
              {{ vm.context.primary }}
            </span>
            <span v-if="vm.context.secondary">{{ vm.context.secondary }}</span>
          </div>
        </div>

        <!-- ── 館方回覆 ── -->
        <div
          v-if="vm.reply"
          class="border-t border-l-4 border-t-neutral-border border-l-brand-primary bg-brand-background p-4 md:p-6"
        >
          <div class="mb-2 flex flex-wrap items-center justify-between gap-2">
            <span class="inline-flex items-center gap-1 font-semibold text-neutral-text-primary">
              <UIcon name="i-lucide-reply" aria-hidden="true" />
              館方回覆
            </span>
            <time class="text-sm text-neutral-text-secondary" :datetime="vm.reply.repliedAt.value">
              {{ vm.reply.repliedAt.full }}
            </time>
          </div>
          <p
            class="mb-3 whitespace-pre-wrap wrap-anywhere text-neutral-text-primary"
            v-text="vm.reply.content"
          ></p>

          <!-- 未表態：三顆按鈕；已表態：顯示結果 -->
          <div v-if="vm.reply.canRateSatisfaction" class="border-t border-neutral-border pt-3">
            <div class="mb-2 text-sm text-neutral-text-secondary">這則回覆對你有幫助嗎？</div>
            <div class="flex flex-wrap gap-2">
              <UButton
                v-for="s in SATISFACTION"
                :key="s.value"
                color="primary"
                variant="outline"
                :icon="s.icon"
                :label="s.text"
                :disabled="savingSatisfaction || isPreview"
                @click="rate(s.value)"
              />
            </div>
          </div>
          <div
            v-else-if="vm.reply.satisfaction != null"
            class="flex items-center gap-1 border-t border-neutral-border pt-3 text-sm text-neutral-text-secondary"
          >
            <UIcon :name="satisfactionOf(vm.reply.satisfaction).icon" aria-hidden="true" />
            你覺得這則回覆：{{ satisfactionOf(vm.reply.satisfaction).text }}
          </div>
        </div>
        <div
          v-else-if="!vm.isSpamMarked"
          class="flex items-center gap-1 border-t border-neutral-border px-4 py-3 text-sm text-neutral-text-secondary md:px-6"
        >
          <UIcon name="i-lucide-hourglass" aria-hidden="true" />
          館方尚未回覆
        </div>
      </article>

      <UAlert
        v-if="actionError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        :title="actionError"
        role="alert"
        class="mt-3"
      />

      <!-- ── 公開切換：放在卡片外面，因為它是「對這則評論的操作」，不是評論內容 ── -->
      <USwitch
        v-if="vm.canToggleVisibility"
        v-model="isPublic"
        :disabled="savingVisibility || isPreview"
        :loading="savingVisibility"
        label="公開這則評論"
        description="關閉後其他顧客就看不到，你隨時可以再打開。"
        class="mt-4"
      />

      <UButton
        :to="{ name: 'review-index' }"
        color="primary"
        variant="outline"
        icon="i-lucide-arrow-left"
        label="返回評論專區"
        class="mt-6"
      />
    </template>
  </main>
</template>
