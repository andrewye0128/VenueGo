<!--
    ReviewWriteView.vue — 撰寫評論
    取代 Views/CReview/CreateForVisit.cshtml 與 CreateForBooking.cshtml（兩個合成一個）

    路由：/reviews/visit/:token/write   → kind='visit',   ticket=QRToken
          /reviews/booking/:id/write    → kind='booking', ticket=ReviewPerBookingId

    ── 兩種評論的差別由後端決定 ─────────────────────────────
    要不要顯示提及標籤、能不能選匿名、要不要顯示公開開關，
    全部看後端回傳的 form 設定（規格 2-2）。這一頁不寫
    「if (kind === 'booking') 就隱藏公開開關」這種判斷——
    「預約評論一律不公開」是業務規則，業務規則只該寫在後端一個地方。
    這一頁只看 kind 決定圖示和標題文字。

    ── 資格判定 ─────────────────────────────────────────
    一進頁面就向後端要表單設定，同時也是在問「這張憑證現在能不能評」：
      查無憑證／不是本人 → 顯示「查無指定評論」
      已過期             → 顯示「超過可以評論的時間」
      已經評過           → 直接轉去檢視頁
    送出時後端會再判一次（表單可能停在畫面上好幾天）。

    ── 驗證（9/29 移植到 Front-Web）──────────────────────────
    前端用 UForm + Zod 先檢查（組裡的做法）；後端 [ApiController] 會再檢查一次。
    後端回來的欄位錯誤用 UForm 的 setErrors 放回對應的欄位底下，兩邊的錯誤顯示在同一個位置。
-->
<script setup>
import { ref, reactive, computed, watch, onMounted } from "vue";
import { useRouter } from "vue-router";
import { z } from "zod";
import { getWriteForm, createReview, fieldErrorsOf } from "@/api/reviewApi";
import { ErrorCodes } from "@/constants/errorCodes";
import { mineRoute } from "@/router/reviewRoutes";
import { kindInfo } from "@/components/review/reviewKinds";
import { useDraft } from "@/composables/useDraft";
import StarInput from "@/components/review/StarInput.vue";
import ReviewKindIcon from "@/components/review/ReviewKindIcon.vue";
import ReviewConfirmModal from "@/components/review/ReviewConfirmModal.vue";

const props = defineProps({
  kind: { type: String, required: true }, // 'visit' | 'booking'
  ticket: { type: String, required: true }, // QRToken 或 ReviewPerBookingId
});

const router = useRouter();
const confirmModal = useOverlay().create(ReviewConfirmModal);

// ── 頁面狀態 ──
const pageState = ref("loading"); // 'loading' | 'blocked' | 'ready'
const blockedMessage = ref("");
const setup = ref(null); // 後端回傳的表單設定（規格 2-2）

// ── 表單內容 ──
// 欄位名稱跟送給後端的 JSON 一模一樣（規格 2-3），送出時不必再轉換。
const state = reactive({
  starRating: null,
  reviewContent: "",
  mentionsVenue: false,
  mentionsStaff: false,
  isAnonymous: false,
  isPublic: true,
});

const maxLength = computed(() => setup.value?.form.contentMaxLength ?? 1000);
const length = computed(() => state.reviewContent.length);
// 上限的九成開始變紅；上限改了門檻會跟著動
const nearLimit = computed(() => length.value >= Math.floor(maxLength.value * 0.9));

// 前端驗證規則。字數上限由後端決定，所以 schema 也跟著後端的值組出來。
const schema = computed(() =>
  z.object({
    starRating: z.number({ error: "請選擇星等" }).int().min(1, "請選擇星等").max(5, "請選擇星等"),
    reviewContent: z.string().max(maxLength.value, `評論內容不可超過 ${maxLength.value} 字`),
  }),
);

const formRef = ref(null);
const formError = ref(""); // 不屬於任何欄位的錯誤
const submitting = ref(false);

// StarInput 是專案自己的元件，不會自動觸發 UForm 的驗證。
// 選了星星之後自己叫 UForm 重新檢查這一欄，「請選擇星等」才會馬上消失。
watch(
  () => state.starRating,
  () => formRef.value?.validate({ name: "starRating", silent: true }),
);

// ── 草稿 ──
const draft = useDraft({
  form: state,
  // key 沿用 draft-box.js 的前綴，加上 v2：舊版草稿的欄位名稱是大寫開頭、值是字串，格式不相容
  key: () => `review-draft:v2:${props.kind}:${props.ticket}`,
  // 只存「這一頁允許使用者改」的欄位：例如未登入時匿名是鎖死的，就不存它
  fields: () => {
    const f = setup.value?.form;
    if (!f) return [];
    const list = ["starRating", "reviewContent"];
    if (f.showMentions) list.push("mentionsVenue", "mentionsStaff");
    if (f.showAnonymous && f.canChooseAnonymous) list.push("isAnonymous");
    if (f.showPublic) list.push("isPublic");
    return list;
  },
  confirmLeave: () =>
    confirmModal.open({
      title: "尚未儲存",
      description: "你填寫的內容還沒有儲存，離開這一頁之後會消失。",
      actions: [
        // 第一顆是預設焦點，放最安全的「留在這頁」
        { value: "cancel", label: "留在這頁", variant: "outline" },
        { value: "discard", label: "不儲存，直接離開", color: "error", variant: "outline" },
        { value: "save", label: "儲存草稿並離開" },
      ],
    }),
  confirmOverwrite: async (when) =>
    (await confirmModal.open({
      title: "覆蓋草稿？",
      description: `已經有一份 ${when} 儲存的草稿，要用現在的內容覆蓋它嗎？`,
      actions: [
        { value: "keep", label: "不要覆蓋", variant: "outline" },
        { value: "overwrite", label: "覆蓋" },
      ],
    })) === "overwrite",
});
// 從 draft 物件裡拿出來，template 裡才會自動解開 .value
const { savedAtText } = draft;

// ── 資格不符時的處理（載入與送出共用）──
// 回傳 true 代表「已經處理掉了，呼叫端不用再做什麼」
function handleEligibility(e) {
  switch (e.errorCode) {
    case ErrorCodes.AlreadyReviewed:
      // 已經評過：草稿沒有用了，清掉，直接去看那則評論
      draft.discard();
      router.replace(mineRoute(props.kind, props.ticket));
      return true;
    case ErrorCodes.NotFound:
    case ErrorCodes.Expired:
    case ErrorCodes.NotLoggedIn: // 預約評論要會員登入
    case ErrorCodes.Forbidden: // 登入的不是會員（例如員工帳號）
      pageState.value = "blocked";
      blockedMessage.value = e.message;
      draft.discard(); // 停止攔截離開；評不了了，草稿留著也沒用
      return true;
    default:
      return false;
  }
}

onMounted(async () => {
  try {
    const data = await getWriteForm(props.kind, props.ticket);
    setup.value = data;
    state.isAnonymous = data.form.initial.isAnonymous;
    state.isPublic = data.form.initial.isPublic;
    pageState.value = "ready";
    draft.start(); // 有草稿就代入；代入之後才開始追蹤「有沒有改過」
  } catch (e) {
    if (!handleEligibility(e)) {
      pageState.value = "blocked";
      blockedMessage.value = e.message;
    }
  }
});

// ── 送出 ──
// UForm 前端驗證通過才會呼叫這裡
async function onSubmit() {
  formError.value = "";
  const choice = await confirmModal.open({
    title: "確定要送出評論嗎？",
    description: "送出後無法修改呦。",
    actions: [
      { value: "cancel", label: "再看一下", variant: "outline" },
      { value: "submit", label: "送出" },
    ],
  });
  if (choice !== "submit") return;

  submitting.value = true;
  try {
    await createReview(props.kind, props.ticket, { ...state });
    draft.discard(); // 先清草稿、停止攔截，下一行換頁才不會跳「尚未儲存」
    // replace 而不是 push：按上一頁不該回到一張已經送出的表單
    router.replace(mineRoute(props.kind, props.ticket));
  } catch (e) {
    if (handleEligibility(e)) return;
    if (e.errorCode === ErrorCodes.ValidationFailed) {
      // 後端的欄位錯誤放回對應的欄位底下；不屬於任何欄位的（key 是 "_"）顯示在表單上方
      const errors = fieldErrorsOf(e);
      const general = errors._ ?? [];
      delete errors._;
      formRef.value?.setErrors(
        Object.entries(errors).map(([name, messages]) => ({ name, message: messages[0] })),
      );
      formError.value = general[0] ?? e.message;
    } else {
      formError.value = e.message;
    }
  } finally {
    submitting.value = false;
  }
}

async function saveDraft(event) {
  const button = event.currentTarget;
  if (await draft.save()) button?.blur(); // 避免按鈕停在 focus 樣式，看起來像沒反應
}

const kindText = computed(() => kindInfo(props.kind)); // 圖示與文字，見 reviewKinds.js

const mentionOptions = [
  { key: "mentionsVenue", label: "提及場地" },
  { key: "mentionsStaff", label: "提及服務" },
];
</script>

<template>
  <main class="mx-auto max-w-xl px-4 py-10 md:px-6">
    <h1 class="mb-1 text-2xl font-bold text-neutral-text-primary">撰寫評論</h1>
    <p class="mb-6 text-sm text-neutral-text-secondary">{{ kindText.subtitle }}</p>

    <p v-if="pageState === 'loading'" class="py-10 text-center text-neutral-text-secondary">
      載入中⋯
    </p>

    <!-- 評不了：查無憑證、過期、需要登入 -->
    <div
      v-else-if="pageState === 'blocked'"
      class="rounded-lg border border-neutral-border bg-neutral-surface px-4 py-10 text-center"
    >
      <UIcon
        name="i-lucide-frown"
        class="mb-3 size-10 text-neutral-text-secondary"
        aria-hidden="true"
      />
      <p class="mb-4 text-neutral-text-primary">{{ blockedMessage }}</p>
      <UButton
        :to="{ name: 'review-index' }"
        color="primary"
        variant="outline"
        label="回評論專區"
      />
    </div>

    <UForm
      v-else
      ref="formRef"
      :schema="schema"
      :state="state"
      class="space-y-6 rounded-lg border border-neutral-border bg-neutral-surface p-4 md:p-6"
      @submit="onSubmit"
    >
      <!-- 確認區塊：你正在評哪一筆 -->
      <div
        v-if="setup.context.primary"
        class="rounded-lg border border-neutral-border bg-brand-background p-3"
      >
        <div class="flex items-center gap-1 font-semibold text-neutral-text-primary">
          <ReviewKindIcon :kind="kind" />
          {{ setup.context.primary }}
        </div>
        <div v-if="setup.context.secondary" class="text-sm text-neutral-text-secondary">
          {{ setup.context.secondary }}
        </div>
      </div>

      <UFormField label="評分" name="starRating" required>
        <StarInput v-model="state.starRating" />
      </UFormField>

      <!-- 從源頭減少個資：公開時雖然會自動遮蔽，但先提醒比事後遮好 -->
      <UFormField
        label="評論內容"
        name="reviewContent"
        description="請勿留下電話、Email 等個人資料；公開顯示時會自動隱藏。"
      >
        <template #hint>
          <span :class="nearLimit ? 'text-semantic-error' : 'text-neutral-text-secondary'">
            {{ length }} / {{ maxLength }}
          </span>
        </template>
        <UTextarea
          v-model="state.reviewContent"
          :rows="8"
          :maxlength="maxLength"
          :placeholder="kindText.placeholder"
          class="w-full"
        />
      </UFormField>

      <UFormField v-if="setup.form.showMentions" label="這則評論提到了⋯（可複選）">
        <div class="flex flex-wrap gap-2">
          <UCheckbox
            v-for="opt in mentionOptions"
            :key="opt.key"
            v-model="state[opt.key]"
            :label="opt.label"
            variant="card"
          />
        </div>
      </UFormField>

      <!-- 匿名：未登入時鎖定為匿名 -->
      <USwitch
        v-if="setup.form.showAnonymous"
        v-model="state.isAnonymous"
        label="匿名發表"
        :disabled="!setup.form.canChooseAnonymous"
        :description="
          setup.form.canChooseAnonymous
            ? '開啟後會以系統給的隨機暱稱顯示，不會出現你的姓名。'
            : '未登入的評論一律匿名，系統會給你一個隨機暱稱。'
        "
      />

      <USwitch
        v-if="setup.form.showPublic"
        v-model="state.isPublic"
        label="公開此評論"
        description="送出後仍可隨時在評論頁切換。"
      />

      <UAlert
        v-if="formError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        :title="formError"
        role="alert"
      />

      <!-- 送出與草稿。
           送出按鈕關掉全站預設的 loadingAuto：送出前要先等使用者在確認視窗按「送出」，
           那段時間按鈕不該轉圈。真正呼叫後端時才用 submitting 顯示轉圈。 -->
      <div class="flex flex-wrap items-center gap-2 border-t border-neutral-border pt-4">
        <UButton
          type="submit"
          color="primary"
          size="lg"
          label="送出"
          :loading-auto="false"
          :loading="submitting"
        />
        <UButton
          color="primary"
          variant="outline"
          size="lg"
          icon="i-lucide-save"
          label="儲存草稿"
          :loading-auto="false"
          @click="saveDraft"
        />
        <UButton
          :to="{ name: 'review-index' }"
          color="primary"
          variant="ghost"
          label="回評論專區"
          class="ms-auto"
        />
        <small v-if="savedAtText" class="w-full text-neutral-text-secondary" aria-live="polite">
          {{ savedAtText }}
        </small>
      </div>
    </UForm>
  </main>
</template>
