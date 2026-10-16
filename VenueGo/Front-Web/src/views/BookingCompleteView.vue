<!--
  場地預約 ③ 預約完成
  資料來源：填寫頁建立預約成功後，用 router.replace({ state: { reservation } }) 帶過來
    存在這一頁的瀏覽紀錄（history.state）裡，不放在網址上：
    - 重新整理：瀏覽器會保留，還看得到
    - 直接打網址、從書籤進來：沒有資料，請客人到「我的預約」查看
  線上付款的「前往付款」是階段 3（模擬付款頁），目前先停用
  UButton、UIcon 由 Nuxt UI 自動匯入
-->
<script setup>
import { computed } from "vue";
import BookingSteps from "@/components/booking/BookingSteps.vue";
import EmptyState from "@/components/EmptyState.vue";
import { PAYMENT_METHOD, PAYMENT_METHOD_LABEL } from "@/constants/payment";
import { formatAmount, formatDateTime, formatDateWithWeekday } from "@/utils/format";

// history.state 是瀏覽器的功能，不是 Vue 的資料，不會變動，讀一次就好
const reservation = window.history.state?.reservation ?? null;

const isOnline = computed(() => reservation?.paymentMethod === PAYMENT_METHOD.ONLINE);
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
          <span v-if="isOnline">
            請在
            <strong class="tabular-nums">{{ formatDateTime(reservation.paymentDueAt) }}</strong>
            前完成線上付款，逾期未付款的預約將自動取消。
          </span>
          <span v-else>
            請在
            <strong class="tabular-nums">{{ formatDateTime(reservation.paymentDueAt) }}</strong>
            前到櫃台付款，逾期未付款的預約將自動取消。
          </span>
        </p>

        <div class="mt-6 flex flex-col gap-2 sm:flex-row sm:justify-center">
          <!-- 階段 3 做模擬付款頁之後再開放 -->
          <UButton v-if="isOnline" color="action" size="lg" disabled title="線上付款功能製作中">
            前往付款（製作中）
          </UButton>
          <UButton
            :to="{ name: 'reservations' }"
            :color="isOnline ? 'primary' : 'action'"
            :variant="isOnline ? 'outline' : 'solid'"
            size="lg"
          >
            查看我的預約
          </UButton>
        </div>
      </section>
    </div>
  </main>
</template>
