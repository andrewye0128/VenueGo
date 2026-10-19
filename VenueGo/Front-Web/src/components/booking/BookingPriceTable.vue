<script setup>
// 費用明細表格：每一格時段一列（時段／類型／單價／小計），最後一列總金額
// 排版參考後台確認頁 Views/ReservationCreate/Confirm.cshtml 的「費用明細」
// 填寫資料頁使用；階段 3 的付款頁也會用到
//
// slots：[{ slotTime: "17:00", unitPrice: 1500, isPeak: true }]（試算 API 回傳的）
// totalAmount：總金額（試算 API 算好的，不在前端自己加總，跟後端一致）
// ⚠️ 這裡的金額只是給客人看的，建立預約時後端會重新計算
import { formatAmount } from "@/utils/format";
import { slotEndTime } from "@/utils/slotSelection";

defineProps({
  slots: { type: Array, required: true },
  totalAmount: { type: Number, required: true },
});
</script>

<template>
  <!-- overflow-x-auto：手機太窄時表格可以左右滑，不會把整頁撐寬 -->
  <div class="overflow-x-auto">
    <table class="w-full text-sm tabular-nums">
      <caption class="sr-only">
        每個時段一列的費用明細
      </caption>
      <thead class="bg-brand-background text-neutral-text-secondary">
        <tr>
          <th scope="col" class="px-2 py-2 sm:px-3 text-start font-normal">時段</th>
          <th scope="col" class="px-2 py-2 text-start font-normal whitespace-nowrap sm:px-3">
            類型
          </th>
          <th scope="col" class="px-2 py-2 sm:px-3 text-end font-normal">單價</th>
          <th scope="col" class="px-2 py-2 sm:px-3 text-end font-normal">小計</th>
        </tr>
      </thead>
      <tbody class="text-neutral-text-primary">
        <tr v-for="slot in slots" :key="slot.slotTime" class="border-t border-neutral-border">
          <td class="px-2 py-2 sm:px-3 whitespace-nowrap">
            {{ slot.slotTime }}–{{ slotEndTime(slot.slotTime) }}
          </td>
          <td class="px-2 py-2 whitespace-nowrap sm:px-3">
            <!-- 尖峰：跟時段按鈕一樣的橘色標籤；離峰用一般灰字 -->
            <span
              v-if="slot.isPeak"
              class="rounded-sm bg-brand-action px-1 text-[11px] leading-4 font-semibold text-white"
            >
              尖峰
            </span>
            <span v-else class="text-neutral-text-secondary">離峰</span>
          </td>
          <td class="px-2 py-2 sm:px-3 text-end whitespace-nowrap">
            {{ formatAmount(slot.unitPrice) }}
          </td>
          <!-- 一格就是一小時，小計等於單價 -->
          <td class="px-2 py-2 sm:px-3 text-end whitespace-nowrap">
            {{ formatAmount(slot.unitPrice) }}
          </td>
        </tr>
      </tbody>
      <tfoot>
        <tr class="border-t border-neutral-border bg-brand-background">
          <td colspan="3" class="px-2 py-2 sm:px-3 text-end text-neutral-text-secondary">總金額</td>
          <td
            class="px-2 py-2 sm:px-3 text-end text-lg font-bold whitespace-nowrap text-brand-primary"
          >
            {{ formatAmount(totalAmount) }}
          </td>
        </tr>
      </tfoot>
    </table>
  </div>
</template>
