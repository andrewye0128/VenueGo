<script setup>
// 付款方式選擇（填寫資料頁）
// options：試算 API 回傳的 paymentOptions：[{ paymentMethod, isAvailable, dueAt, unavailableReason }]
//   能不能選、期限都是後端算好的，這裡只負責顯示，付款規則不在前端再寫一份
// 不能選的選項變灰，並寫出原因（狀態不能只靠顏色，Design System 3.4）
// v-model：選中的 paymentMethod（constants/payment.js 的 PAYMENT_METHOD）
// URadioGroup 由 Nuxt UI 自動匯入
import { computed } from "vue";
import { PAYMENT_METHOD, PAYMENT_METHOD_LABEL } from "@/constants/payment";
import { formatDateTime } from "@/utils/format";

const model = defineModel({ type: Number, default: undefined });

const props = defineProps({
  options: { type: Array, required: true },
});

// 每個付款方式下方的說明
function describe(option) {
  if (!option.isAvailable) return option.unavailableReason;
  const due = formatDateTime(option.dueAt);
  return option.paymentMethod === PAYMENT_METHOD.ONLINE
    ? `信用卡付款，請在 ${due} 前完成`
    : `請在 ${due} 前到櫃台付款`;
}

// 線上付款排前面：任何日期都能選
const items = computed(() =>
  [...props.options]
    .sort((a, b) => b.paymentMethod - a.paymentMethod)
    .map((option) => ({
      value: option.paymentMethod,
      label: PAYMENT_METHOD_LABEL[option.paymentMethod],
      description: describe(option),
      disabled: !option.isAvailable,
    })),
);
</script>

<template>
  <!-- variant="card"：每個選項是一張可以整張點的卡片，比小圓點好點（手機尤其明顯） -->
  <URadioGroup
    v-model="model"
    :items="items"
    variant="card"
    :ui="{ fieldset: 'grid gap-2 sm:grid-cols-2', item: 'w-full' }"
  />
</template>
