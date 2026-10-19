<!--
  場地預約 ② 填寫資料
  網址：/booking/confirm?venueId=7&date=2026-10-20&slots=17:00,18:00（需要登入，路由守衛會先擋）
  流程：
    1. 讀網址參數 → 呼叫試算 API：後端重新檢查時段、算金額、回傳能用的付款方式
    2. 客人填使用人數、付款方式、發票，勾選同意條款
    3. 按「確認預約」→ 建立預約 API → 帶著回傳結果到完成頁（/booking/complete）
  金額只是顯示用：建立預約時不送金額，由後端重新計算
  排版參考後台確認頁 Views/ReservationCreate/Confirm.cshtml：
    左邊一張大卡片（預約人 → 場地與時段＋人數 → 費用明細 → 付款與發票 → 確認事項）
    右邊「預約資訊摘要」（電腦版捲動時固定，放「上一步」和「確認預約」）；最下面再放一顆「確認預約」
    手機：大卡片 → 摘要（含「上一步」）→ 確認預約，由上往下
  UForm、UFormField、UInputNumber、UCheckbox、UButton、UAlert、useToast 由 Nuxt UI 自動匯入
-->
<script setup>
import { computed, reactive, ref, watch } from "vue";
import { RouterLink, useRoute, useRouter } from "vue-router";
import BookingSteps from "@/components/booking/BookingSteps.vue";
import BookingConfirmSummary from "@/components/booking/BookingConfirmSummary.vue";
import BookingPriceTable from "@/components/booking/BookingPriceTable.vue";
import ConfirmSection from "@/components/booking/ConfirmSection.vue";
import InvoiceFields from "@/components/booking/InvoiceFields.vue";
import PaymentMethodPicker from "@/components/booking/PaymentMethodPicker.vue";
import TermsModal from "@/components/booking/TermsModal.vue";
import EmptyState from "@/components/EmptyState.vue";
import { getBookingQuote } from "@/api/bookingApi";
import { createReservation } from "@/api/reservationApi";
import { ErrorCodes } from "@/constants/errorCodes";
import { INVOICE_TYPE } from "@/constants/payment";
import { createBookingConfirmSchema } from "@/schemas/bookingConfirm";
import { useMemberStore } from "@/stores/member";
import { formatDateWithWeekday } from "@/utils/format";

const route = useRoute();
const router = useRouter();
const toast = useToast();
const memberStore = useMemberStore();

// 場地沒有設定容納人數時的人數上限（跟後台確認頁的 MaxPersonAmount 一樣）
const DEFAULT_MAX_PERSON = 20;

// ── 網址參數 ──
// slots 是 "17:00,18:00"：拆開、去掉重複、排序；格式不對的整筆當作無效
const request = computed(() => {
  const venueId = Number(route.query.venueId);
  const date = route.query.date;
  const slots = [...new Set(String(route.query.slots ?? "").split(","))].sort();
  const valid =
    Number.isInteger(venueId) &&
    venueId > 0 &&
    typeof date === "string" &&
    /^\d{4}-\d{2}-\d{2}$/.test(date) &&
    slots.length > 0 &&
    slots.every((s) => /^\d{2}:00$/.test(s));
  return valid ? { venueId, date, slots } : null;
});

// 回預約頁重選：帶回原本的運動、日期（試算失敗時不知道運動，就只帶日期）
const backToBooking = computed(() => ({
  name: "booking",
  query: {
    sportTypeId: quote.value?.sportTypeId,
    date: request.value?.date,
  },
}));

// ── 試算 ──
const quote = ref(null);
const loading = ref(false);
const loadError = ref(""); // 可以重試的錯誤（例如連不到伺服器）
// 不能繼續的情況（時段被訂走、場地暫停開放…）：{ title, description }
const blocked = ref(null);

// 試算失敗時，哪些錯誤代表「這筆預約不能繼續」，要請客人回去重選
const BLOCKING_ERRORS = {
  [ErrorCodes.SlotUnavailable]: "這個時段已無法預約",
  [ErrorCodes.SlotTaken]: "這個時段剛被預約走了",
  [ErrorCodes.VenueNotBookable]: "這個場地目前暫停開放預約",
  [ErrorCodes.DateOutOfRange]: "這個日期目前無法預約",
};

async function loadQuote() {
  if (!request.value) return;
  loading.value = true;
  loadError.value = "";
  try {
    quote.value = await getBookingQuote(request.value);
  } catch (err) {
    if (BLOCKING_ERRORS[err.errorCode]) {
      blocked.value = { title: BLOCKING_ERRORS[err.errorCode], description: err.message };
    } else {
      loadError.value = err.message || "讀取預約資料失敗，請稍後再試";
    }
  } finally {
    loading.value = false;
  }
}

// ── 表單 ──
const state = reactive({
  personAmount: 1,
  paymentMethod: undefined,
  invoiceType: INVOICE_TYPE.PAPER,
  carrierNo: "",
  agree: false,
});

const member = computed(() => memberStore.member);
const maxPerson = computed(() => quote.value?.capacity ?? DEFAULT_MAX_PERSON);
const availableMethods = computed(
  () => quote.value?.paymentOptions.filter((o) => o.isAvailable).map((o) => o.paymentMethod) ?? [],
);
const schema = computed(() =>
  createBookingConfirmSchema({
    maxPerson: maxPerson.value,
    paymentMethods: availableMethods.value,
    invoiceType: state.invoiceType,
  }),
);

// 會員資料有載具：預設選手機條碼並帶入（大部分人每次都用同一個載具，客人可以改）
// 只在第一次讀到會員資料時帶入，之後客人改了就不再覆蓋
let carrierFilled = false;
watch(
  member,
  (value) => {
    if (!value || carrierFilled) return;
    carrierFilled = true;
    if (value.carrierNo) {
      state.invoiceType = INVOICE_TYPE.MOBILE_BARCODE;
      state.carrierNo = value.carrierNo;
    }
  },
  { immediate: true },
);

// 試算結果更新時（第一次讀到、付款規則改變重新試算）：
// 目前選的付款方式不能用了，就改選第一個能用的（線上付款任何時候都能用）
watch(availableMethods, (methods) => {
  if (!methods.includes(state.paymentMethod)) state.paymentMethod = methods[0];
});

// 路由守衛已經讀過會員資料；這裡是保險（例如守衛之後才登出）
if (!memberStore.member) {
  memberStore.fetchCurrentMember().catch(() => {});
}
loadQuote();

// 場地沒有設定容納人數時，提示文字跟後台一樣說明「尚未設定」
const capacityHint = computed(() =>
  quote.value?.capacity != null
    ? `此場地可容納 ${quote.value.capacity} 人`
    : `此場地尚未設定可容納人數，最多 ${DEFAULT_MAX_PERSON} 人`,
);

// ── 送出 ──
const termsOpen = ref(false);
const submitting = ref(false);
const submitError = ref("");

// UForm 檢查全部通過才會呼叫；event.data 是檢查後的資料（載具已經去掉空白、轉大寫）
async function onSubmit(event) {
  const data = event.data;
  submitting.value = true;
  submitError.value = "";
  try {
    const reservation = await createReservation({
      venueId: quote.value.venueId,
      bookingDate: quote.value.bookingDate,
      slotTimes: quote.value.slots.map((s) => s.slotTime),
      personAmount: data.personAmount,
      paymentMethod: data.paymentMethod,
      invoiceType: data.invoiceType,
      // 選紙本發票時不送載具（輸入框藏起來時值還在，避免送出客人不想用的載具）
      carrierNo: data.invoiceType === INVOICE_TYPE.MOBILE_BARCODE ? data.carrierNo : null,
      termsAccepted: data.agree,
      termsVersion: quote.value.termsVersion,
    });
    // replace：完成頁按「上一頁」不會回到填寫頁（避免以為還沒送出、又按一次）
    // state：資料存在這一頁的瀏覽紀錄裡，不放網址；重新整理還在，換一台電腦打開網址就沒有
    await router.replace({ name: "booking-complete", state: { reservation } });
  } catch (err) {
    await handleSubmitError(err);
  } finally {
    submitting.value = false;
  }
}

// 檢查沒通過：捲到第一個有錯的欄位
// 右側摘要的「確認預約」按鈕離左邊的欄位很遠，不捲過去的話，客人看不到哪裡填錯
function onValidationError(event) {
  const firstId = event.errors[0]?.id;
  const element = firstId && document.getElementById(firstId);
  if (!element) return;
  element.focus({ preventScroll: true });
  element.scrollIntoView({ behavior: "smooth", block: "center" });
}

async function handleSubmitError(err) {
  switch (err.errorCode) {
    case ErrorCodes.SlotTaken:
    case ErrorCodes.SlotUnavailable:
    case ErrorCodes.VenueNotBookable:
    case ErrorCodes.DateOutOfRange:
      blocked.value = { title: BLOCKING_ERRORS[err.errorCode], description: err.message };
      return;
    case ErrorCodes.PaymentMethodNotAllowed:
      // 例如停留太久跨過午夜，現場付款的條件變了：重新試算，付款方式會自動改成能用的
      toast.add({ title: err.message, color: "warning", icon: "i-mdi-alert-outline" });
      await loadQuote();
      return;
    case ErrorCodes.TermsVersionMismatch:
      // 條款改版：重新試算拿新版本，取消勾選，請客人重新閱讀
      state.agree = false;
      toast.add({ title: err.message, color: "warning", icon: "i-mdi-alert-outline" });
      await loadQuote();
      return;
    case ErrorCodes.NotLoggedIn:
      // 登入過期：帶去登入頁，登入後回到這一頁（填的資料會遺失，選擇還在網址上）
      await router.push({ name: "login", query: { redirect: route.fullPath } });
      return;
    default:
      submitError.value = err.message || "預約失敗，請稍後再試";
  }
}
</script>

<template>
  <main class="mx-auto max-w-7xl px-4 py-6 md:px-6 md:py-10">
    <h1 class="text-2xl font-bold text-neutral-text-primary">場地預約</h1>
    <div class="mt-4">
      <BookingSteps :current="2" />
    </div>

    <div class="mt-6">
      <!-- 網址參數不完整（例如自己改網址） -->
      <div v-if="!request" class="rounded border border-neutral-border bg-neutral-surface">
        <EmptyState
          icon="i-mdi-calendar-question"
          title="預約資料不完整"
          description="請回到預約頁重新選擇場地與時段。"
          :actions="[{ label: '回預約頁', to: { name: 'booking' } }]"
        />
      </div>

      <!-- 不能繼續：時段被訂走、場地暫停開放… -->
      <div v-else-if="blocked" class="rounded border border-neutral-border bg-neutral-surface">
        <EmptyState
          icon="i-mdi-calendar-remove-outline"
          :title="blocked.title"
          :description="blocked.description"
          :actions="[{ label: '回去重選時段', to: backToBooking }]"
        />
      </div>

      <UAlert
        v-else-if="loadError"
        color="error"
        variant="soft"
        icon="i-mdi-alert-circle"
        :description="loadError"
        :actions="[{ label: '重新載入', color: 'error', variant: 'outline', onClick: loadQuote }]"
      />

      <p
        v-else-if="loading && !quote"
        class="rounded border border-neutral-border bg-neutral-surface py-16 text-center text-sm text-neutral-text-secondary"
      >
        載入中…
      </p>

      <!-- 填寫表單：整個版面包在 UForm 裡，右側摘要和最下面的「確認預約」都是同一張表單的送出按鈕
           電腦：左 2/3 大卡片、右 1/3 摘要（捲動時固定）；手機：大卡片 → 摘要 → 按鈕 -->
      <UForm
        v-else-if="quote"
        :schema="schema"
        :state="state"
        @submit="onSubmit"
        @error="onValidationError"
      >
        <div class="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)] lg:items-start">
          <!-- ── 左：確認預約資料 ── -->
          <div class="rounded border border-neutral-border bg-neutral-surface p-4 md:p-6">
            <h2 class="text-lg font-semibold text-neutral-text-primary">確認預約資料</h2>
            <p class="mt-1 text-sm text-neutral-text-secondary">
              請確認以下預約資訊，確認無誤後送出預約。
            </p>

            <div class="mt-4 flex flex-col gap-4">
              <!-- 預約人：記在會員本人名下，這裡只顯示；要改請到會員中心 -->
              <ConfirmSection icon="i-mdi-account-outline" title="預約人">
                <template #actions>
                  <RouterLink
                    :to="{ name: 'profile' }"
                    class="text-sm text-brand-primary underline-offset-2 hover:underline"
                  >
                    修改會員資料
                  </RouterLink>
                </template>
                <dl v-if="member" class="grid gap-3 text-sm sm:grid-cols-2">
                  <div>
                    <dt class="text-neutral-text-secondary">姓名</dt>
                    <dd class="text-neutral-text-primary">{{ member.name }}</dd>
                  </div>
                  <div>
                    <dt class="text-neutral-text-secondary">手機</dt>
                    <dd class="text-neutral-text-primary tabular-nums">{{ member.phone }}</dd>
                  </div>
                  <div class="sm:col-span-2">
                    <dt class="text-neutral-text-secondary">Email</dt>
                    <dd class="break-all text-neutral-text-primary">{{ member.email }}</dd>
                  </div>
                </dl>
                <p v-else class="text-sm text-neutral-text-secondary">載入會員資料中…</p>
              </ConfirmSection>

              <!-- 場地與時段（唯讀）＋使用人數（唯一要填的） -->
              <ConfirmSection icon="i-mdi-home-outline" title="場地與時段">
                <dl class="grid gap-3 text-sm sm:grid-cols-2">
                  <div>
                    <dt class="text-neutral-text-secondary">場地</dt>
                    <dd class="text-neutral-text-primary">
                      {{ [quote.sportName, quote.venueName].filter(Boolean).join("・") }}
                    </dd>
                  </div>
                  <div>
                    <dt class="text-neutral-text-secondary">使用日期</dt>
                    <dd class="text-neutral-text-primary">
                      {{ formatDateWithWeekday(quote.bookingDate) }}
                    </dd>
                  </div>
                  <div>
                    <dt class="text-neutral-text-secondary">使用時段</dt>
                    <dd class="text-neutral-text-primary tabular-nums">
                      {{ quote.startTime }}–{{ quote.endTime }}（{{ quote.slots.length }} 小時）
                    </dd>
                  </div>
                </dl>

                <hr class="my-4 border-neutral-border" />

                <UFormField label="使用人數" name="personAmount" required :help="capacityHint">
                  <UInputNumber
                    v-model="state.personAmount"
                    :min="1"
                    :max="maxPerson"
                    :increment="{ 'aria-label': '增加人數' }"
                    :decrement="{ 'aria-label': '減少人數' }"
                    class="w-40"
                  />
                </UFormField>
              </ConfirmSection>

              <!-- 費用明細：表格貼齊框線（flush） -->
              <ConfirmSection icon="i-mdi-file-document-outline" title="費用明細" flush>
                <BookingPriceTable :slots="quote.slots" :total-amount="quote.totalAmount" />
              </ConfirmSection>

              <ConfirmSection icon="i-mdi-credit-card-outline" title="付款與發票">
                <div class="flex flex-col gap-5">
                  <UFormField label="付款方式" name="paymentMethod" required>
                    <PaymentMethodPicker
                      v-model="state.paymentMethod"
                      :options="quote.paymentOptions"
                    />
                  </UFormField>
                  <InvoiceFields
                    v-model:invoice-type="state.invoiceType"
                    v-model:carrier-no="state.carrierNo"
                  />
                </div>
              </ConfirmSection>

              <ConfirmSection icon="i-mdi-checkbox-marked-outline" title="確認事項">
                <UFormField name="agree">
                  <div class="flex flex-wrap items-center gap-x-1 text-sm">
                    <!-- 不加 required：UCheckbox 的 required 會打開瀏覽器內建檢查，
                         在 UForm 檢查之前就跳出瀏覽器自己的提示框，跟其他欄位的錯誤樣式不一致；
                         必填由 schema 的 mustAgreeRule 檢查，星號另外顯示 -->
                    <UCheckbox v-model="state.agree" label="我已閱讀並同意" />
                    <!-- 條款連結放在勾選框文字外面：點連結是打開條款，不會順便打勾 -->
                    <button
                      type="button"
                      class="cursor-pointer text-brand-primary underline underline-offset-2"
                      @click="termsOpen = true"
                    >
                      租借條款（{{ quote.termsVersion }}）
                    </button>
                    <span class="text-semantic-error" aria-hidden="true">*</span>
                  </div>
                </UFormField>
              </ConfirmSection>
            </div>
          </div>

          <!-- ── 右：預約資訊摘要（電腦版捲動時固定在畫面上） ──
               top-32（128px）：頁首 AppHeader 也是 sticky（約 117px 高），要固定在頁首下方，不然會被蓋住 -->

          <BookingConfirmSummary
            :quote="quote"
            :member="member"
            :form="state"
            class="lg:sticky lg:top-32"
          >
            <!-- 「上一步」在「確認預約」上方：
                   電腦、手機都顯示（手機只有這裡有「上一步」）
                 「確認預約」電腦版才顯示：手機的摘要下面緊接著就是按鈕列，不用重複 -->
            <template #actions>
              <UButton
                :to="backToBooking"
                color="primary"
                variant="outline"
                size="lg"
                block
                icon="i-mdi-arrow-left"
                class="mt-4"
              >
                上一步：選擇時段
              </UButton>
              <UButton
                type="submit"
                color="action"
                size="lg"
                block
                class="mt-3 flex"
                :loading="submitting"
              >
                {{ submitting ? "送出中…" : "確認預約" }}
              </UButton>
            </template>
          </BookingConfirmSummary>
        </div>

        <UAlert
          v-if="submitError"
          color="error"
          variant="soft"
          icon="i-mdi-alert-circle"
          :description="submitError"
          class="mt-6"
        />

        <!-- ── 最下面的按鈕列（跟後台一樣靠右）；手機滿版 ──
             「上一步：選擇時段」放在右側摘要的「確認預約」上方 -->
        <!-- <div
          class="mt-6 flex flex-col gap-3 border-t border-neutral-border pt-4 sm:flex-row sm:justify-end"
        > -->
        <!-- loading：送出中轉圈並停用，防止連按兩次訂到兩筆 -->
        <!-- <UButton
            type="submit"
            color="action"
            size="lg"
            trailing-icon="i-mdi-arrow-right"
            class="justify-center"
            :loading="submitting"
          >
            {{ submitting ? "送出中…" : "確認預約" }}
          </UButton>
        </div> -->
      </UForm>
    </div>

    <TermsModal v-if="quote" v-model:open="termsOpen" :version="quote.termsVersion" />
  </main>
</template>
