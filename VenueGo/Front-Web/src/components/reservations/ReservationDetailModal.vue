<!--
  ReservationDetailModal.vue — 預約明細視窗

  用法（useOverlay 是 Nuxt UI 的函式，不用 import）：
    const detailModal = useOverlay().create(ReservationDetailModal);
    const choice = await detailModal.open({ reservation, cancellable: true });
    // choice：按「取消預約」→ "cancel"（由頁面接著打開取消視窗）；按「關閉」、Esc、點外面 → undefined

  cancellable：要不要顯示「取消預約」按鈕，由頁面用 canCancel(reservation, 網站時間) 算好傳進來
  （判斷要用 useSiteClock 的網站時間，交給頁面處理，這裡只負責顯示）

  為什麼不在這裡直接取消：取消的流程（確認視窗、呼叫 API、重新載入列表）由頁面統一處理，
  表格、手機卡片、明細視窗三個入口都走同一段程式
-->
<script setup>
import { computed } from "vue";
import StatusBadge from "@/components/list/StatusBadge.vue";
import { PAYMENT_STATUS, RESERVATION_STATUS } from "@/constants/statuses";
import { formatAmount, formatDate, formatDateTime, formatTimeRange } from "@/utils/format";
import { formatPersonCount, formatReservationNo } from "@/utils/reservation";

const props = defineProps({
  reservation: { type: Object, required: true },
  cancellable: { type: Boolean, default: false },
});

const emit = defineEmits(["close"]);

const r = computed(() => props.reservation);

// 一般欄位：左邊欄位名稱、右邊值
const rows = computed(() => [
  { label: "場地名稱", value: r.value.venueName },
  { label: "運動類型", value: r.value.sportName },
  { label: "預約日期", value: formatDate(r.value.bookingDate) },
  { label: "使用時段", value: formatTimeRange(r.value.startTime, r.value.endTime) },
  // 人數、金額：預約沒有訂單時是 null，顯示「—」
  { label: "預約人數", value: formatPersonCount(r.value.personCount) },
  { label: "金額", value: formatAmount(r.value.totalAmount) || "—" },
]);

// 只在特定狀態才顯示的欄位
const extraRows = computed(() =>
  [
    // 未付款（0）才需要知道付款期限
    r.value.paymentStatus === 0 && {
      label: "付款期限",
      value: formatDateTime(r.value.paymentDueAt),
    },
    { label: "建立時間", value: formatDateTime(r.value.reservedAt) },
    r.value.cancelledAt && { label: "取消時間", value: formatDateTime(r.value.cancelledAt) },
    r.value.cancelledAt && { label: "取消原因", value: r.value.cancelReason || "—" },
  ].filter(Boolean),
);
</script>

<template>
  <UModal title="預約明細" :description="formatReservationNo(r.reservationId)">
    <template #body>
      <dl class="grid grid-cols-[auto_1fr] gap-x-6 gap-y-3 text-sm">
        <template v-for="item in rows" :key="item.label">
          <dt class="text-neutral-text-secondary">{{ item.label }}</dt>
          <dd class="text-neutral-text-primary">{{ item.value }}</dd>
        </template>

        <dt class="self-center text-neutral-text-secondary">預約狀態</dt>
        <dd><StatusBadge :status="RESERVATION_STATUS[r.reservationStatus]" /></dd>
        <dt class="self-center text-neutral-text-secondary">付款狀態</dt>
        <dd>
          <StatusBadge v-if="r.paymentStatus != null" :status="PAYMENT_STATUS[r.paymentStatus]" />
          <span v-else class="text-neutral-text-primary">—</span>
        </dd>

        <template v-for="item in extraRows" :key="item.label">
          <dt class="text-neutral-text-secondary">{{ item.label }}</dt>
          <dd class="text-neutral-text-primary">{{ item.value }}</dd>
        </template>
      </dl>
    </template>

    <template #footer>
      <div class="flex w-full flex-col gap-2 md:flex-row md:justify-end">
        <UButton
          label="關閉"
          color="primary"
          variant="outline"
          class="w-full md:w-auto"
          @click="emit('close')"
        />
        <UButton
          v-if="cancellable"
          label="取消預約"
          color="error"
          variant="outline"
          class="w-full md:w-auto"
          @click="emit('close', 'cancel')"
        />
      </div>
    </template>
  </UModal>
</template>
