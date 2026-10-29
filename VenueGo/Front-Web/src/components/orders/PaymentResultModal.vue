<!--
  PaymentResultModal.vue — 付款結果視窗（從綠界付完錢，回到「訂單與付款」頁時跳出）

  用法（useOverlay 是 Nuxt UI 的函式，不用 import）：
    const resultModal = useOverlay().create(PaymentResultModal);
    const choice = await resultModal.open({ order });
    // choice：按「重新付款」→ "pay"、按「查看明細」→ "detail"（由頁面接著處理）；
    //         按「關閉」、Esc、點外面 → undefined

  order：列表的那一筆。視窗打開後會向後端查最新的狀態（GET /api/orders/{orderId}），不用列表的狀態：
    列表是頁面打開時讀的，之後狀態變了它不會知道
  確認中：每 PENDING_POLL_INTERVAL_MS 重查一次，最多等 PENDING_POLL_TIMEOUT_MS；視窗關掉就停
  顯示什麼由 utils/paymentResult.js 決定，這裡只負責查詢和畫面
-->
<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { getMyOrder } from "@/api/orderApi";
import { useSiteClock } from "@/composables/useSiteClock";
import {
  CHECKING_VIEW,
  describePaymentResult,
  PENDING_POLL_INTERVAL_MS,
  PENDING_POLL_TIMEOUT_MS,
  UNAVAILABLE_VIEW,
} from "@/utils/paymentResult";

const props = defineProps({
  order: { type: Object, required: true },
});

const emit = defineEmits(["close"]);

// 網站時間：決定還能不能重新付款（跟付款按鈕的倒數一致）
const { now } = useSiteClock();

// ── 查詢最新的狀態 ──
const latest = ref(null); // 後端查到的訂單；還沒查到是 null
const timedOut = ref(false); // 已經等超過 PENDING_POLL_TIMEOUT_MS

const view = computed(() => {
  if (latest.value) {
    return describePaymentResult(latest.value, now.value, { timedOut: timedOut.value });
  }
  return timedOut.value ? UNAVAILABLE_VIEW : CHECKING_VIEW;
});

// 「等了多久」用真實時間量：網站時間會跟著時光機跳，量出來的等待時間就不準了
const startedAt = Date.now();
let timer = null;

async function refresh() {
  try {
    latest.value = await getMyOrder(props.order.orderId);
  } catch {
    // 這一次沒查到（例如網路不穩）不顯示錯誤，下一次再查；一直查不到，最後會顯示 UNAVAILABLE_VIEW
  }

  if (!view.value.isPending) return; // 有結果了，不用再查
  if (Date.now() - startedAt >= PENDING_POLL_TIMEOUT_MS) {
    timedOut.value = true;
    return;
  }
  timer = setTimeout(refresh, PENDING_POLL_INTERVAL_MS);
}

onMounted(refresh);
onBeforeUnmount(() => clearTimeout(timer));
</script>

<template>
  <UModal title="付款結果" :description="order.orderNo" :close="{ 'aria-label': '關閉' }">
    <template #body>
      <!-- role="status"：內容從「確認中」變成「付款成功」時，讀屏軟體會唸出新的結果 -->
      <div class="flex flex-col items-center gap-3 py-2 text-center" role="status">
        <UIcon :name="view.icon" :class="['size-14', view.iconClass]" aria-hidden="true" />
        <p class="text-lg font-bold text-neutral-text-primary">{{ view.title }}</p>
        <p class="text-sm text-neutral-text-secondary">{{ view.description }}</p>
      </div>
    </template>

    <!-- 按鈕排法同 OrderDetailModal：手機直排滿版，電腦靠右；主要動作放最後 -->
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
          v-if="view.canViewDetail"
          label="查看明細"
          color="primary"
          class="w-full justify-center md:w-auto"
          @click="emit('close', 'detail')"
        />
        <UButton
          v-if="view.canRetry"
          label="重新付款"
          color="action"
          class="w-full justify-center md:w-auto"
          @click="emit('close', 'pay')"
        />
      </div>
    </template>
  </UModal>
</template>
