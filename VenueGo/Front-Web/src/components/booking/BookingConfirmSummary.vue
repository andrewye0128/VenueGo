<script setup>
// 填寫資料頁右邊的「預約資訊摘要」
// 排版參考後台 Views/Shared/Components/ReservationSummary/Default.cshtml
// 跟著左邊的輸入即時更新：使用人數、付款方式、付款期限、發票、載具
//
// quote：試算 API 的回傳（場地、日期、時段、金額、付款方式與期限）
// member：會員資料（/me），還沒讀到是 null
// form：表單目前的值 { personAmount, paymentMethod, invoiceType, carrierNo }
// 插槽 actions：摘要最下面的按鈕（電腦版放「確認預約」，捲動時摘要固定在畫面上，隨時按得到）
// UIcon 由 Nuxt UI 自動匯入
import { computed } from "vue";
import { INVOICE_TYPE, INVOICE_TYPE_LABEL, PAYMENT_METHOD_LABEL } from "@/constants/payment";
import { formatAmount, formatDateTime, formatDateWithWeekday } from "@/utils/format";

const props = defineProps({
  quote: { type: Object, required: true },
  member: { type: Object, default: null },
  form: { type: Object, required: true },
});

// 選中的付款方式的期限（試算 API 算好的）
const dueAt = computed(
  () => props.quote.paymentOptions.find((o) => o.paymentMethod === props.form.paymentMethod)?.dueAt,
);

// 兩組資料：上面是「訂什麼」、下面是「怎麼付」，中間用分隔線隔開（跟後台一樣）
const bookingRows = computed(() => [
  { icon: "i-mdi-account-outline", label: "預約人", value: props.member?.name ?? "—" },
  {
    icon: "i-mdi-home-outline",
    label: "場地",
    value: [props.quote.sportName, props.quote.venueName].filter(Boolean).join("・"),
  },
  {
    icon: "i-mdi-calendar-blank-outline",
    label: "日期",
    value: formatDateWithWeekday(props.quote.bookingDate),
  },
  {
    icon: "i-mdi-clock-outline",
    label: "時段",
    value: `${props.quote.startTime}–${props.quote.endTime}（${props.quote.slots.length} 小時）`,
  },
  {
    icon: "i-mdi-account-group-outline",
    label: "使用人數",
    // 輸入框清空時是 undefined/null，顯示「—」而不是「undefined 人」
    value: props.form.personAmount ? `${props.form.personAmount} 人` : "—",
  },
]);

const paymentRows = computed(() => {
  const isBarcode = props.form.invoiceType === INVOICE_TYPE.MOBILE_BARCODE;
  const rows = [
    {
      icon: "i-mdi-currency-usd",
      label: "金額",
      value: formatAmount(props.quote.totalAmount),
      strong: true,
    },
    {
      icon: "i-mdi-credit-card-outline",
      label: "付款方式",
      value: PAYMENT_METHOD_LABEL[props.form.paymentMethod] ?? "—",
    },
    // 送出前一定是「未付款」，顯示「什麼時候前要付」對客人比較有用（後台這裡是付款狀態）
    {
      icon: "i-mdi-clock-alert-outline",
      label: "付款期限",
      value: dueAt.value ? `${formatDateTime(dueAt.value)} 前` : "—",
    },
    {
      icon: "i-mdi-receipt-text-outline",
      label: "發票",
      value: INVOICE_TYPE_LABEL[props.form.invoiceType],
    },
  ];
  // 載具只在選手機條碼時顯示；顯示轉成大寫後的樣子（送出時也會轉大寫）
  if (isBarcode) {
    rows.push({
      icon: "i-mdi-barcode",
      label: "載具號碼",
      value: props.form.carrierNo?.trim().toUpperCase() || "—",
    });
  }
  return rows;
});
</script>

<template>
  <aside
    aria-labelledby="confirm-summary-heading"
    class="rounded border border-neutral-border bg-neutral-surface"
  >
    <h1
      id="confirm-summary-heading"
      class="flex items-center justify-center gap-2 border-b border-neutral-border px-4 py-3 font-semibold text-neutral-text-primary text-base"
    >
      <UIcon name="i-mdi-calendar-check-outline" class="size-5 text-brand-primary" />
      預約資訊摘要
    </h1>

    <div class="p-4">
      <dl class="flex flex-col gap-2 text-sm">
        <div v-for="row in bookingRows" :key="row.label" class="flex items-baseline gap-3">
          <dt class="flex w-24 shrink-0 items-center gap-1.5 text-neutral-text-secondary">
            <UIcon :name="row.icon" class="size-4 shrink-0 self-center" />
            {{ row.label }}
          </dt>
          <dd class="min-w-0 wrap-break-word text-neutral-text-primary tabular-nums">
            {{ row.value }}
          </dd>
        </div>
      </dl>

      <hr class="my-3 border-neutral-border" />

      <dl class="flex flex-col gap-2 text-sm">
        <div v-for="row in paymentRows" :key="row.label" class="flex items-baseline gap-3">
          <dt class="flex w-24 shrink-0 items-center gap-1.5 text-neutral-text-secondary">
            <UIcon :name="row.icon" class="size-4 shrink-0 self-center" />
            {{ row.label }}
          </dt>
          <dd
            class="min-w-0 wrap-break-word tabular-nums"
            :class="
              row.strong ? 'text-lg font-bold text-brand-primary' : 'text-neutral-text-primary'
            "
          >
            {{ row.value }}
          </dd>
        </div>
      </dl>

      <!-- 送出後的下一步：保留時段、要在期限前付款（後台這裡是給管理員的提示） -->
      <p
        role="note"
        class="mt-4 flex gap-2 rounded bg-brand-primary/8 p-3 text-xs text-neutral-text-primary"
      >
        <UIcon name="i-mdi-information-outline" class="mt-0.5 size-4 shrink-0 text-brand-primary" />
        <span>送出後會為您保留時段，請在付款期限前完成付款，逾期未付款的預約將自動取消。</span>
      </p>

      <slot name="actions" />
    </div>
  </aside>
</template>
