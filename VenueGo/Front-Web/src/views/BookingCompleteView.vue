<!--
  場地預約 ③ 預約完成
  資料來源：填寫頁建立預約成功後，用 router.replace({ state: { reservation } }) 帶過來
    存在這一頁的瀏覽紀錄（history.state）裡，不放在網址上：
    - 重新整理：瀏覽器會保留，還看得到
    - 直接打網址、從書籤進來：沒有資料，請客人到「我的預約」查看
  線上付款的「前往付款」：
    - 建立預約的回傳只有訂單編號，沒有訂單 Id，而且是建立那一刻的狀態
      → 打開頁面時用訂單編號向後端查這一筆（[GET] /api/orders?keyword=），拿到訂單 Id 和最新的付款狀態
    - 能不能付款用 canPay 判斷（同「訂單與付款」頁），按鈕顯示「剩 N 分鐘」，時間到了自動消失
    - 付款流程（開付款單、跳到綠界、錯誤處理）由 useOrderPayment 處理，跟「訂單與付款」頁共用
  UButton、UIcon 由 Nuxt UI 自動匯入
-->
<script setup>
import { computed, onMounted, ref } from "vue";
import BookingSteps from "@/components/booking/BookingSteps.vue";
import EmptyState from "@/components/EmptyState.vue";
import { getMyOrders } from "@/api/orderApi";
import { useOrderPayment } from "@/composables/useOrderPayment";
import { useSiteClock } from "@/composables/useSiteClock";
import { PAYMENT_METHOD, PAYMENT_METHOD_LABEL } from "@/constants/payment";
import { PAYMENT_STATUS_CODE } from "@/constants/statuses";
import { formatAmount, formatDateTime, formatDateWithWeekday } from "@/utils/format";
import { canPay, formatRemaining } from "@/utils/order";

// history.state 是瀏覽器的功能，不是 Vue 的資料，不會變動，讀一次就好
const reservation = window.history.state?.reservation ?? null;

const isOnline = computed(() => reservation?.paymentMethod === PAYMENT_METHOD.ONLINE);

// ── 線上付款：向後端查這筆訂單的最新狀態 ──
// 網站時間：「剩 N 分鐘」會自己倒數，時間到了付款按鈕自動消失（同「訂單與付款」頁）
const { now } = useSiteClock();

const order = ref(null); // 後端查到的訂單（有 orderId、最新的付款狀態、最晚開始付款時間）
const orderStatus = ref("loading"); // "loading" 查詢中、"ready" 查到了、"error" 查不到

async function loadOrder() {
  try {
    const data = await getMyOrders({ keyword: reservation.orderNo, pageSize: 1 });
    // keyword 是「包含」：用完整的訂單編號查，最多只會有這一筆，還是比對一次比較保險
    order.value = data.items.find((item) => item.orderNo === reservation.orderNo) ?? null;
    orderStatus.value = order.value ? "ready" : "error";
  } catch {
    // 查不到也不擋客人：付款按鈕不顯示，提示到「訂單與付款」頁付款
    orderStatus.value = "error";
  }
}
onMounted(() => {
  if (isOnline.value) loadOrder();
});

// 付款狀態可能變了（另一個分頁付過了、從綠界按上一頁回來）就重新查一次
const { isBusy, isPaying, pay } = useOrderPayment({ onStateChanged: loadOrder });

const payable = computed(() => order.value !== null && canPay(order.value, now.value));
// 查詢中先顯示（轉圈、不能按），查到之後可以付才留著
const showPayButton = computed(
  () => isOnline.value && (orderStatus.value === "loading" || payable.value),
);

// 付款期限提示框的情況：付款處理中、已付款時，不再提醒「請在期限前付款」
const paymentStage = computed(() => {
  if (!isOnline.value) return "onsite";
  const status = order.value?.paymentStatus;
  if (status === PAYMENT_STATUS_CODE.PROCESSING) return "processing";
  if (status === PAYMENT_STATUS_CODE.PAID) return "paid";
  // 查到了但不能付（超過最晚開始付款時間）：不能再叫客人「請在期限前付款」，付款按鈕已經沒了
  if (orderStatus.value === "ready" && !payable.value) return "closed";
  return "due";
});
const details = computed(() => {
  if (!reservation) return [];
  const r = reservation;
  return [
    { label: "場地", value: [r.sportName, r.venueName].filter(Boolean).join("・") },
    { label: "日期", value: formatDateWithWeekday(r.bookingDate) },
    { label: "時段", value: `${r.startTime}–${r.endTime}` },
    { label: "人數", value: `${r.personAmount} 人` },
    { label: "金額", value: formatAmount(r.totalAmount) },
    { label: "付款方式", value: PAYMENT_METHOD_LABEL[r.paymentMethod] },
  ];
});
</script>

<template>
  <main class="mx-auto max-w-7xl px-4 py-6 md:px-6 md:py-10">
    <h1 class="text-2xl font-bold text-neutral-text-primary">場地預約</h1>
    <div class="mt-4">
      <BookingSteps :current="3" />
    </div>

    <div class="mt-6">
      <!-- 沒有資料：直接打網址、從書籤進來 -->
      <div v-if="!reservation" class="rounded border border-neutral-border bg-neutral-surface">
        <EmptyState
          icon="i-mdi-clipboard-text-search-outline"
          title="找不到這次的預約資料"
          description="如果已經完成預約，可以到「我的預約」查看。"
          :actions="[
            { label: '查看我的預約', to: { name: 'reservations' } },
            { label: '回預約頁', to: { name: 'booking' }, color: 'primary', variant: 'outline' },
          ]"
        />
      </div>

      <section
        v-else
        aria-labelledby="complete-heading"
        class="mx-auto max-w-xl rounded border border-neutral-border bg-neutral-surface p-6 md:p-8"
      >
        <div class="text-center">
          <!-- 圖示是 inline 元素，text-center 不一定置中，改成 block＋mx-auto -->
          <UIcon name="i-mdi-check-circle" class="mx-auto block size-14 text-semantic-success" />
          <h2 id="complete-heading" class="mt-2 text-xl font-bold text-neutral-text-primary">
            預約成功
          </h2>
          <p class="mt-1 text-sm text-neutral-text-secondary">
            訂單編號 <span class="font-semibold tabular-nums">{{ reservation.orderNo }}</span>
          </p>
        </div>

        <dl
          class="mt-6 grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 rounded bg-brand-background p-4 text-sm"
        >
          <template v-for="item in details" :key="item.label">
            <dt class="text-neutral-text-secondary">{{ item.label }}</dt>
            <dd class="font-medium text-neutral-text-primary tabular-nums">{{ item.value }}</dd>
          </template>
        </dl>

        <!-- 付款期限：最重要的下一步，用提醒框標出來 -->
        <p
          class="mt-4 flex items-start gap-2 rounded border border-semantic-warning/40 bg-semantic-warning/10 p-3 text-sm text-neutral-text-primary"
        >
          <UIcon
            name="i-mdi-clock-alert-outline"
            class="mt-0.5 size-5 shrink-0 text-semantic-warning"
          />
          <span v-if="paymentStage === 'onsite'">
            請在
            <strong class="tabular-nums">{{ formatDateTime(reservation.paymentDueAt) }}</strong>
            前到櫃台付款，逾期未付款的預約將自動取消。
          </span>
          <span v-else-if="paymentStage === 'processing'">
            付款確認中，結果確認後會顯示在「訂單與付款」頁。
          </span>
          <span v-else-if="paymentStage === 'paid'">已完成付款，預約已確認。</span>
          <span v-else-if="paymentStage === 'closed'">
            已超過可以開始線上付款的時間，逾期未付款的預約將自動取消。
          </span>
          <span v-else>
            請在
            <strong class="tabular-nums">{{ formatDateTime(reservation.paymentDueAt) }}</strong>
            前完成線上付款，逾期未付款的預約將自動取消。
            <!-- 查不到訂單時沒有付款按鈕，告訴客人去哪裡付 -->
            <template v-if="orderStatus === 'error'">請到「訂單與付款」頁付款。</template>
          </span>
        </p>

        <div class="mt-6 flex flex-col gap-2 sm:flex-row sm:justify-center">
          <!-- 轉圈、停用由 useOrderPayment 控制（原因見 useOrderPayment.js），所以關掉 loadingAuto -->
          <UButton
            v-if="showPayButton"
            color="action"
            size="lg"
            :loading="orderStatus === 'loading' || (order !== null && isPaying(order))"
            :disabled="isBusy || order === null"
            :loading-auto="false"
            @click="pay(order)"
          >
            前往付款
            <template v-if="payable"
              >（{{ formatRemaining(order.paymentStartDeadline, now) }}）</template
            >
          </UButton>
          <!-- 沒有付款按鈕時，「查看我的預約」就是主要的按鈕 -->
          <UButton
            :to="{ name: 'reservations' }"
            :color="showPayButton ? 'primary' : 'action'"
            :variant="showPayButton ? 'outline' : 'solid'"
            size="lg"
          >
            查看我的預約
          </UButton>
        </div>
      </section>
    </div>
  </main>
</template>
