<!--
  ReviewKindIcon.vue — 評論種類的圖示（現場＝地圖釘，預約＝收據）
  用法：<ReviewKindIcon :kind="vm.kind" />

  為什麼獨立成元件、不寫在 reviewKinds.js：
  Nuxt UI 只掃描 .vue 檔，找出用到哪些圖示再打包進來（vite.config.js 的 icon.clientBundle.scan）。
  寫在 .js 裡的圖示名稱掃不到，執行時會改向網路要圖示，連不上就是一片空白。
-->
<script setup>
import { computed } from "vue";

const props = defineProps({
  kind: { type: String, required: true },
});

// 每一種都明確列出來，不認得的種類直接丟錯誤（理由同 reviewKinds.js）
const ICONS = {
  visit: "i-lucide-map-pin",
  booking: "i-lucide-receipt",
};

const name = computed(() => {
  const icon = ICONS[props.kind];
  if (!icon) throw new Error(`[ReviewKindIcon] 不認得的評論種類：${props.kind}`);
  return icon;
});
</script>

<template>
  <UIcon :name="name" aria-hidden="true" />
</template>
