<!--
  OrderDetailModal.vue — 訂單明細視窗（會員中心「訂單與付款」）

  用法（useOverlay 是 Nuxt UI 的函式，不用 import）：
    const detailModal = useOverlay().create(OrderDetailModal);
    const choice = await detailModal.open({ order });
    // choice：按「付款」→ "pay"（由頁面接著開始付款流程）；按「關閉」、Esc、點外面 → undefined

  order：列表的那一筆（先顯示列表已有的欄位，費用明細、付款紀錄打開後才向後端讀取）
  為什麼不在這裡直接付款：付款流程（3-C 綠界）由頁面統一處理，表格、手機卡片、明細視窗三個入口都走同一段程式

  「付款」按鈕跟列表一樣用 canPay 判斷，現在時間用 useSiteClock：
  視窗開著的時候時間到了，按鈕會自動消失
-->
<script setup>
import { computed, onMounted, ref } from "vue";
import BookingPriceTable from "@/components/booking/BookingPriceTable.vue";
import StatusBadge from "@/components/list/StatusBadge.vue";
import { getMyOrder } from "@/api/orderApi";
import { useSiteClock } from "@/composables/useSiteClock";
import {
  INVOICE_TYPE,
  INVOICE_TYPE_LABEL,
  PAYMENT_CHANNEL_LABEL,
  PAYMENT_METHOD_LABEL,
} from "@/constants/payment";
import { PAYMENT_STATUS, RESERVATION_STATUS } from "@/constants/statuses";
import { formatDateTime, formatDateWithWeekday, formatTimeRange } from "@/utils/format";
import { canPay, formatRemaining } from "@/utils/order";
import { formatPersonCount, formatReservationNo } from "@/utils/reservation";

const props = defineProps({
  order: { type: Object, required: true },
});

const emit = defineEmits(["close"]);

const { now } = useSiteClock();

// ── 明細：打開時才讀（列表不帶費用明細、付款紀錄，資料量比較小）──
const detail = ref(null);
const loading = ref(true);
const errorMessage = ref("");

async function load() {
  loading.value = true;
  errorMessage.value = "";
  try {
    detail.value = await getMyOrder(props.order.orderId);
  } catch (err) {
    errorMessage.value = err.message || "讀取訂單明細失敗，請稍後再試";
  } finally {
    loading.value = false;
  }
}
onMounted(load);

// 明細讀到之前先用列表的資料，視窗一打開就有內容，不會整個空白
const o = computed(() => detail.value ?? props.order);
const payable = computed(() => canPay(o.value, now.value));

const rows = computed(() => [
  { label: "預約編號", value: formatReservationNo(o.value.reservationId) },
  { label: "場地", value: [o.value.sportName, o.value.venueName].filter(Boolean).join("・") },
  {
    label: "使用時間",
    value: `${formatDateWithWeekday(o.value.bookingDate)} ${formatTimeRange(o.value.startTime, o.value.endTime)}`,
  },
  { label: "下單時間", value: formatDateTime(o.value.orderCreatedAt) },
  // 下面兩個是明細才有的欄位，還沒讀到時顯示「—」
  { label: "人數", value: detail.value ? formatPersonCount(detail.value.personCount) : "—" },
  {
    label: "發票",
    value: !detail.value
      ? "—"
      : detail.value.invoiceType === INVOICE_TYPE.MOBILE_BARCODE
        ? `${INVOICE_TYPE_LABEL[detail.value.invoiceType]} ${detail.value.carrierNo ?? ""}`
        : INVOICE_TYPE_LABEL[detail.value.invoiceType],
  },
]);
</script>

<template>
  <UModal title="訂單明細" :description="o.orderNo" :close="{ 'aria-label': '關閉' }" scrollable>
    <template #body>
      <div class="flex flex-col gap-5">
        <dl class="grid grid-cols-[auto_1fr] gap-x-6 gap-y-3 text-sm">
          <template v-for="item in rows" :key="item.label">
            <dt class="text-neutral-text-secondary">{{ item.label }}</dt>
            <dd class="text-neutral-text-primary tabular-nums">{{ item.value }}</dd>
          </template>
          <dt class="self-center text-neutral-text-secondary">預約狀態</dt>
          <dd><StatusBadge :status="RESERVATION_STATUS[o.reservationStatus]" /></dd>
        </dl>

        <UAlert
          v-if="errorMessage"
          color="error"
          variant="soft"
          icon="i-mdi-alert-circle"
          :description="errorMessage"
          :actions="[{ label: '重新載入', color: 'error', variant: 'outline', onClick: load }]"
        />
        <p v-else-if="loading" class="py-6 text-center text-sm text-neutral-text-secondary">
          載入明細中…
        </p>

        <template v-else-if="detail">
          <!-- 費用明細：跟填寫資料頁同一個表格（金額是下單當時的價格） -->
          <section aria-labelledby="order-lines-heading">
            <h3
              id="order-lines-heading"
              class="mb-2 text-sm font-semibold text-neutral-text-primary"
            >
              費用明細
            </h3>
            <div class="overflow-hidden rounded border border-neutral-border">
              <BookingPriceTable :slots="detail.lines" :total-amount="detail.totalAmount" />
            </div>
          </section>

          <!-- 付款紀錄：目前一筆；3-C 綠界串接後，付款失敗再付一次會有多筆，新的在上面 -->
          <section aria-labelledby="order-payments-heading">
            <h3
              id="order-payments-heading"
              class="mb-2 text-sm font-semibold text-neutral-text-primary"
            >
              付款紀錄
            </h3>
            <ul class="flex flex-col gap-2">
              <li
                v-for="(payment, index) in detail.payments"
                :key="index"
                class="flex flex-col gap-1 rounded border border-neutral-border p-3 text-sm"
              >
                <div class="flex items-center justify-between gap-2">
                  <span class="font-medium text-neutral-text-primary">
                    {{ PAYMENT_METHOD_LABEL[payment.paymentMethod] }}・{{
                      PAYMENT_CHANNEL_LABEL[payment.paymentChannel]
                    }}
                  </span>
                  <StatusBadge :status="PAYMENT_STATUS[payment.paymentStatus]" />
                </div>
                <p class="text-neutral-text-secondary tabular-nums">
                  <template v-if="payment.paidAt">
                    {{ formatDateTime(payment.paidAt) }} 付款
                  </template>
                  <template v-else>期限 {{ formatDateTime(payment.paymentDueAt) }}</template>
                </p>
              </li>
            </ul>
          </section>
        </template>
      </div>
    </template>

    <template #footer>
      <div class="flex w-full flex-col gap-2 md:flex-row md:justify-end">
        <UButton
          label="關閉"
          color="primary"
          variant="outline"
          class="w-full justify-center md:w-auto"
          @click="emit('close')"
        />
        <UButton
          v-if="payable"
          color="action"
          class="w-full justify-center md:w-auto"
          @click="emit('close', 'pay')"
        >
          付款（{{ formatRemaining(o.paymentStartDeadline, now) }}）
        </UButton>
      </div>
    </template>
  </UModal>
</template>
