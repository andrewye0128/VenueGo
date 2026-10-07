<script setup>
// 狀態標籤：淡色底膠囊＋左側圖示（例如「✔ 已確認」）
// 用法：<StatusBadge :status="RESERVATION_STATUS[row.reservationStatus]" />
// status 是 constants/statuses.js 對照表裡的一項：{ label, color, icon }
// UBadge 由 Nuxt UI 自動匯入
import { computed } from "vue";

const props = defineProps({
  status: { type: Object, default: null },
});

// 後端新增了狀態、前端對照表還沒補時，對照結果是 undefined：顯示灰色「未知狀態」，不讓畫面出錯
const UNKNOWN = { label: "未知狀態", color: "neutral", icon: "i-mdi-help-circle-outline" };
const badge = computed(() => props.status ?? UNKNOWN);
</script>

<template>
  <UBadge
    :label="badge.label"
    :color="badge.color"
    :icon="badge.icon"
    variant="soft"
    class="gap-1.5 rounded-full px-2.5 py-1 font-semibold"
  />
</template>
