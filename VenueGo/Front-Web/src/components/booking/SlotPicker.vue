<script setup>
// 時段按鈕：顯示一個場地的時段，點一下就 emit("select", "18:00")，選取規則由頁面處理（utils/slotSelection.js）
//
// slots：[{ slotTime, availability, unitPrice, isPeak }]（已經依「時段篩選」過濾）
// selectedTimes：目前選中的時段 ["18:00", "19:00"]
// UIcon 由 Nuxt UI 自動匯入
import { SLOT_AVAILABILITY, SLOT_AVAILABILITY_LABEL } from "@/constants/statuses";
import { formatAmount } from "@/utils/format";
import { isSelectableSlot, slotEndTime } from "@/utils/slotSelection";

const props = defineProps({
  slots: { type: Array, required: true },
  selectedTimes: { type: Array, default: () => [] },
});

const emit = defineEmits(["select"]);

const isSelected = (slot) => props.selectedTimes.includes(slot.slotTime);
// 能不能選：可預約而且有價格（規則在 utils/slotSelection.js，場地卡片的「剩幾格」也用同一條）
const isAvailable = isSelectableSlot;

// 不能選的原因：可預約但沒有價格 →「價格未設定」；其他照狀態顯示（已預約、維護中、已過時間）
const unavailableLabel = (slot) =>
  slot.availability === SLOT_AVAILABILITY.AVAILABLE
    ? "價格未設定"
    : SLOT_AVAILABILITY_LABEL[slot.availability];

// 讀屏軟體唸的內容：「18:00 到 19:00，尖峰，NT$ 600，已選擇」或「12:00 到 13:00，已預約，無法選取」
function ariaLabel(slot) {
  const time = `${slot.slotTime} 到 ${slotEndTime(slot.slotTime)}`;
  if (!isAvailable(slot)) return `${time}，${unavailableLabel(slot)}，無法選取`;
  return `${time}${slot.isPeak ? "，尖峰" : ""}，${formatAmount(slot.unitPrice)}${isSelected(slot) ? "，已選擇" : ""}`;
}
</script>

<template>
  <!-- 手機一排 2 個、平板 3 個、寬的時候 4 個 -->
  <ul class="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4">
    <li v-for="slot in slots" :key="slot.slotTime">
      <!-- 不能選的時段用 disabled：鍵盤 Tab 會跳過、讀屏軟體會唸出「無法使用」 -->
      <button
        type="button"
        :disabled="!isAvailable(slot)"
        :aria-pressed="isSelected(slot)"
        :aria-label="ariaLabel(slot)"
        class="relative flex h-16 w-full cursor-pointer flex-col items-center justify-center rounded border text-sm transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary disabled:cursor-not-allowed"
        :class="
          isSelected(slot)
            ? 'border-brand-primary bg-brand-primary text-white'
            : isAvailable(slot)
              ? 'border-neutral-border bg-neutral-surface text-neutral-text-primary hover:border-brand-primary hover:bg-brand-primary/8'
              : 'border-neutral-border bg-brand-background text-neutral-text-secondary/70'
        "
        @click="emit('select', slot.slotTime)"
      >
        <!-- 已選擇：右上角白色小圓圈＋藍色勾，不佔用時間那一行，時間文字不會被推歪 -->
        <span
          v-if="isSelected(slot)"
          aria-hidden="true"
          class="absolute top-1.5 right-1.5 flex size-5 items-center justify-center rounded-full bg-white text-brand-primary"
        >
          <UIcon name="i-mdi-check" class="size-3.5" />
        </span>

        <span class="font-semibold tabular-nums" aria-hidden="true">
          {{ slot.slotTime }}–{{ slotEndTime(slot.slotTime) }}
        </span>

        <!-- 第二行：價格＋尖峰標籤（不能選的時段改顯示狀態文字）
             尖峰標籤跟價格同一行、不用絕對定位：按鈕變窄時也不會壓到時間文字
             選擇前後都是橘底白字（brand-action）：選了變藍底時，尖峰仍然一眼看得出來 -->
        <span class="mt-0.5 flex items-center gap-1.5 text-xs" aria-hidden="true">
          <template v-if="isAvailable(slot)">
            {{ formatAmount(slot.unitPrice) }}
            <span
              v-if="slot.isPeak"
              class="rounded-sm bg-brand-action px-1 text-[11px] leading-4 font-semibold text-white"
            >
              尖峰
            </span>
          </template>
          <template v-else>{{ unavailableLabel(slot) }}</template>
        </span>
      </button>
    </li>
  </ul>
</template>
