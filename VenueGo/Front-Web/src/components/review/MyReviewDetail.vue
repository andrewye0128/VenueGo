<!--
    MyReviewDetail.vue — 一則「我的評論」的完整內容（10/10 從 views/review/MyReviewView.vue 抽出來）

    用在兩個地方，畫面和行為完全一樣：
      1. views/review/MyReviewView.vue：單筆頁（/reviews/visit/:id、/reviews/booking/:orderId、員工預覽）
      2. views/member/MyReviewsView.vue：會員中心「我的評論 › 已評論」展開後的內容
    抽出來之前，清單要嘛整份複製一次（改一邊忘另一邊），要嘛只能「點了跳到另一頁」。

    用法：
      <MyReviewDetail kind="visit" ticket="12" />
      <MyReviewDetail preview-id="35" />            （員工預覽）
      <MyReviewDetail ... @reply-viewed="..." @visibility-changed="(isPublic) => ..." />
      讀取失敗時想放按鈕：<template #error-action><UButton ... /></template>

    載入、記錄已讀、公開切換、表態的邏輯和說明都沒改，搬過來而已（原本的說明見 MyReviewView.vue 檔頭）。
    外框（標題、預覽提示、返回按鈕）留在使用的頁面：單筆頁和清單需要的不一樣。

    ── 10/12：compact 模式＋館方回覆改成留言串樣式 ──────────────────
    compact（「我的評論」清單展開時用）：清單那一列已經有星等、日期、場地時段、回覆狀態，
    展開區不再重複這些，只放完整內容、館方回覆、表態與公開切換；「館方回覆了你的評論」提示也改成回覆旁的「新」標籤。
    館方回覆（兩種模式都一樣）：改成 Reddit／TikTok 那種「頭像＋對話泡泡」的留言串，
    一眼看得出「這是館方回給你的」，而不是同一張卡片裡的另一段文字。
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
import { MENTION_TAGS, REPLY_SATISFACTION } from "@/constants/review";
import { kindInfo } from "@/utils/review/reviewKinds";
import StarView from "@/components/review/StarView.vue";

// 顧客模式傳 kind + ticket；預覽模式只傳 previewId（種類由 API 回傳的 kind 決定）
const props = defineProps({
  kind: { type: String, default: null },
  ticket: { type: String, default: null },
  previewId: { type: String, default: null },
  // 10/12：「我的評論」清單展開時用，不重複清單那一列已經顯示的資訊
  compact: { type: Boolean, default: false },
});

// 讓外層（單筆頁、「我的評論」清單）知道狀態變了，清單上的標籤才能跟著更新，不必整份重讀
//   reply-viewed：已記錄「顧客看過回覆」→ 清單的「新回覆」標籤拿掉
//   visibility-changed：公開狀態切換成功（參數是新的 isPublic）
//   load-error：讀取失敗（參數是 ErrorCodes），外層依原因決定要放哪些按鈕（10/23）
const emit = defineEmits(["reply-viewed", "visibility-changed", "load-error"]);

const vm = ref(null);
const loadError = ref("");
const actionError = ref("");

// 打開頁面那一刻「是不是第一次看到回覆」。之後就算記錄成已讀，這一次的提示也保留。
const showNewReplyBanner = ref(false);

const isPreview = computed(() => props.previewId != null);

// 錯誤由呼叫端處理：onMounted 顯示在整頁，rate() 顯示在操作區
async function load() {
  return isPreview.value
    ? await getReviewPreview(props.previewId)
    : await getMyReview(props.kind, props.ticket);
}

// 滿意度的三個選項在 constants/review.js
const satisfactionOf = (v) => REPLY_SATISFACTION.find((s) => s.value === v);

onMounted(async () => {
  try {
    vm.value = await load();
  } catch (e) {
    loadError.value = e.message;
    emit("load-error", e.errorCode);
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
        emit("reply-viewed");
      } catch (e) {
        // 這是營運分析用的紀錄，失敗了不該打擾顧客。後端會有 log；前端留一行 warn 方便開發時看到。
        console.warn("[MyReviewDetail] 已讀紀錄沒有存到：", e);
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
      emit("visibility-changed", data.isPublic);
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
      try {
        vm.value = await load();
      } catch {
        // 重讀失敗：至少確定「已經表態過」，先把三顆按鈕停用，避免使用者一直按、一直收到同一個錯誤。
        // 錯誤訊息維持上面那句（比「讀取失敗」更有用），重新整理頁面就會看到正確的結果。
        vm.value.reply.canRateSatisfaction = false;
      }
    }
  } finally {
    savingSatisfaction.value = false;
  }
}
</script>

<template>
  <div>
    <div
      v-if="loadError"
      class="rounded-lg border border-neutral-border bg-neutral-surface px-4 py-10 text-center"
    >
      <UIcon
        name="i-lucide-frown"
        class="mx-auto mb-3 size-10 text-neutral-text-secondary"
        aria-hidden="true"
      />
      <p class="text-neutral-text-primary">{{ loadError }}</p>
      <!-- 錯誤時要不要放按鈕、放什麼，由使用的地方決定（單筆頁放「回我的評論」，清單裡不放） -->
      <div v-if="$slots['error-action']" class="mt-4">
        <slot name="error-action" />
      </div>
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
      <!-- compact 時不顯示：清單那一列已經有「館方新回覆」標籤，回覆旁邊也有「新」 -->
      <UAlert
        v-else-if="showNewReplyBanner && !compact"
        color="primary"
        variant="subtle"
        icon="i-lucide-message-circle-more"
        title="館方回覆了你的評論"
        class="mb-4"
      />

      <!-- ── 評論卡片 ── -->
      <!-- compact 時不包卡片：外面的清單列已經是框了 -->
      <article
        :class="
          compact
            ? ''
            : 'overflow-hidden rounded-lg border border-neutral-border bg-neutral-surface'
        "
      >
        <div :class="compact ? 'pt-1' : 'p-4 md:p-6'">
          <!-- 星等、名稱、日期：compact 時清單那一列已經有了 -->
          <div v-if="!compact" class="mb-2 flex flex-wrap items-start justify-between gap-2">
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

          <!-- compact：清單那一列沒有顯示名稱，這裡補一行「其他人看到的名字」 -->
          <p
            v-if="compact"
            class="mb-2 flex flex-wrap items-center gap-2 text-sm text-neutral-text-secondary"
          >
            <span>顯示名稱：{{ vm.displayName }}</span>
            <UBadge
              v-if="vm.isAnonymous"
              color="neutral"
              variant="outline"
              size="sm"
              icon="i-mdi-incognito"
              label="匿名"
            />
          </p>

          <div v-if="vm.mentionsVenue || vm.mentionsStaff" class="mb-3 flex flex-wrap gap-1">
            <UBadge
              v-if="vm.mentionsVenue"
              :color="MENTION_TAGS.venue.color"
              variant="soft"
              :icon="MENTION_TAGS.venue.icon"
              :label="MENTION_TAGS.venue.label"
            />
            <UBadge
              v-if="vm.mentionsStaff"
              :color="MENTION_TAGS.staff.color"
              variant="soft"
              :icon="MENTION_TAGS.staff.icon"
              :label="MENTION_TAGS.staff.label"
            />
          </div>

          <!-- 系統補的資訊：現場評論是場地與時段，預約評論是訂單編號 -->
          <div
            v-if="vm.context.primary && !compact"
            class="flex flex-wrap items-center gap-x-2 gap-y-1 border-t border-neutral-border pt-2 text-sm text-neutral-text-secondary"
          >
            <span class="inline-flex items-center gap-1">
              <!-- 種類用 API 回傳的（預覽模式下網址上沒有種類） -->
              <UIcon :name="kindInfo(vm.kind).icon" aria-hidden="true" />
              {{ vm.context.primary }}
            </span>
            <span v-if="vm.context.secondary">{{ vm.context.secondary }}</span>
          </div>
        </div>

        <!-- 尚未回覆：只在單筆頁顯示；compact 時清單那一列已經寫了「尚未回覆」 -->
        <div
          v-if="!vm.reply && !vm.isSpamMarked && !compact"
          class="flex items-center gap-1 border-t border-neutral-border px-4 py-3 text-sm text-neutral-text-secondary md:px-6"
        >
          <UIcon name="i-lucide-hourglass" aria-hidden="true" />
          館方尚未回覆
        </div>
      </article>

      <!-- ── 館方回覆（10/12 改成留言串）：館方頭像＋對話泡泡，接在評論下面 ──
           原本是卡片裡左邊一條藍線的一段，跟評論內容很難分開。 -->
      <div v-if="vm.reply" class="mt-3 flex gap-2.5" :class="compact ? '' : 'md:pl-6'">
        <span
          class="flex size-8 shrink-0 items-center justify-center rounded-full bg-brand-primary text-sm font-bold text-white"
          aria-hidden="true"
        >
          館
        </span>
        <div
          class="min-w-0 flex-1 rounded-lg rounded-tl-none border border-neutral-border bg-neutral-surface p-3 md:p-4"
        >
          <div class="mb-1 flex flex-wrap items-center justify-between gap-2">
            <span class="inline-flex items-center gap-2 font-semibold text-neutral-text-primary">
              館方回覆
              <!-- 第一次看到這則回覆時標「新」（compact 時取代上面的提示條） -->
              <UBadge v-if="showNewReplyBanner" color="primary" size="sm" label="新" />
            </span>
            <time class="text-xs text-neutral-text-secondary" :datetime="vm.reply.repliedAt.value">
              {{ vm.reply.repliedAt.full }}
            </time>
          </div>
          <p
            class="whitespace-pre-wrap wrap-anywhere text-neutral-text-primary"
            v-text="vm.reply.content"
          ></p>

          <!-- 未表態：三顆按鈕；已表態：顯示結果 -->
          <div v-if="vm.reply.canRateSatisfaction" class="mt-3 border-t border-neutral-border pt-3">
            <div class="mb-2 text-sm text-neutral-text-secondary">這則回覆對你有幫助嗎？</div>
            <div class="flex flex-wrap gap-2">
              <UButton
                v-for="s in REPLY_SATISFACTION"
                :key="s.value"
                color="primary"
                variant="outline"
                size="sm"
                :icon="s.icon"
                :label="s.text"
                :disabled="savingSatisfaction || isPreview"
                @click="rate(s.value)"
              />
            </div>
          </div>
          <div
            v-else-if="vm.reply.satisfaction != null"
            class="mt-3 flex items-center gap-1 border-t border-neutral-border pt-3 text-sm text-neutral-text-secondary"
          >
            <UIcon :name="satisfactionOf(vm.reply.satisfaction).icon" aria-hidden="true" />
            你覺得這則回覆：{{ satisfactionOf(vm.reply.satisfaction).text }}
          </div>
        </div>
      </div>

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
        :class="compact ? 'mt-3' : 'mt-4'"
      />
    </template>
  </div>
</template>
