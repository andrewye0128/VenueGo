<script setup>
// 已選時段的明細：場地、日期、時段、每一格的價格（尖峰加標籤）、合計
// BookingSummary 的電腦版右側欄、手機版抽屜共用這一份，內容只寫一次
//
// summary：{ venueName, sportName, dateLabel, slots: [{ slotTime, unitPrice, isPeak }] }（一定有值）
// ⚠️ 這裡的金額只是給使用者看的，送出預約時後端會重新計價（不採用前端的金額）
import { computed } from "vue";
import { formatAmount } from "@/utils/format";
import { slotEndTime } from "@/utils/slotSelection";

const props = defineProps({
  summary: { type: Object, required: true },
});

const total = computed(() => props.summary.slots.reduce((sum, s) => sum + s.unitPrice, 0));
const timeRange = computed(() => {
  const slots = props.summary.slots;
  return `${slots[0].slotTime}–${slotEndTime(slots[slots.length - 1].slotTime)}`;
});
</script>

<template>
  <dl class="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
    <dt class="text-neutral-text-secondary">場地</dt>
    <dd class="text-neutral-text-primary">{{ summary.sportName }}・{{ summary.venueName }}</dd>
    <dt class="text-neutral-text-secondary">日期</dt>
    <dd class="text-neutral-text-primary">{{ summary.dateLabel }}</dd>
    <dt class="text-neutral-text-secondary">時段</dt>
    <dd class="text-neutral-text-primary tabular-nums">
      {{ timeRange }}（{{ summary.slots.length }} 小時）
    </dd>
  </dl>

  <ul class="mt-3 border-t border-neutral-border pt-3 text-sm">
    <li
      v-for="slot in summary.slots"
      :key="slot.slotTime"
      class="flex items-center justify-between py-1 text-neutral-text-secondary tabular-nums"
    >
      <span class="flex items-center gap-1.5">
        {{ slot.slotTime }}–{{ slotEndTime(slot.slotTime) }}
        <!-- 尖峰：跟時段按鈕一樣的橘色標籤 -->
        <span
          v-if="slot.isPeak"
          class="rounded-sm bg-brand-action px-1 text-[11px] leading-4 font-semibold text-white"
        >
          尖峰
        </span>
      </span>
      <span>{{ formatAmount(slot.unitPrice) }}</span>
    </li>
  </ul>

  <p
    class="mt-2 flex items-baseline justify-between border-t border-neutral-border pt-2 font-semibold text-neutral-text-primary"
  >
    <span>合計</span>
    <span class="text-lg tabular-nums">{{ formatAmount(total) }}</span>
  </p>
</template>
