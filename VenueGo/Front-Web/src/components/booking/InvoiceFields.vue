<script setup>
// 發票欄位（填寫資料頁）：紙本／手機條碼二選一，選手機條碼才出現載具輸入框
// 要放在 UForm 裡面：載具的錯誤訊息靠 UFormField name="carrierNo" 顯示
// v-model:invoice-type：constants/payment.js 的 INVOICE_TYPE
// v-model:carrier-no：載具號碼（預設值由頁面帶入會員資料的載具）
// URadioGroup、UFormField、UInput 由 Nuxt UI 自動匯入
import { INVOICE_TYPE, INVOICE_TYPE_LABEL } from "@/constants/payment";

const invoiceType = defineModel("invoiceType", { type: Number, required: true });
const carrierNo = defineModel("carrierNo", { type: String, default: "" });

const items = [INVOICE_TYPE.PAPER, INVOICE_TYPE.MOBILE_BARCODE].map((value) => ({
  value,
  label: INVOICE_TYPE_LABEL[value],
}));
</script>

<template>
  <div class="flex flex-col gap-3">
    <UFormField label="發票類型" name="invoiceType" required>
      <URadioGroup
        v-model="invoiceType"
        :items="items"
        variant="card"
        :ui="{ fieldset: 'grid gap-2 sm:grid-cols-2', item: 'w-full' }"
      />
    </UFormField>

    <!-- 切回紙本時，輸入框藏起來但值保留：客人切來切去不用重打；送出時頁面只在選手機條碼才送載具 -->
    <UFormField
      v-if="invoiceType === INVOICE_TYPE.MOBILE_BARCODE"
      label="載具號碼"
      name="carrierNo"
      required
      hint="例如 /AB12345"
    >
      <UInput
        v-model="carrierNo"
        placeholder="/"
        maxlength="8"
        autocomplete="off"
        autocapitalize="characters"
        class="w-full sm:w-60"
      />
    </UFormField>
  </div>
</template>
