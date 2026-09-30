<!--
  StarView.vue — 唯讀星等
  用法：<StarView :rating="4" />

  顏色：main.css 的 rating-star（跟後台的星星同色）；空的星星用 neutral-border。
-->
<script setup>
defineProps({
  rating: { type: Number, required: true },
  // 想放大時傳 Tailwind 的字級 class，例如 "text-xl"
  sizeClass: { type: String, default: "text-base" },
});
</script>

<template>
  <!-- 星星本身是裝飾，讀螢幕的人聽到的是 aria-label 那一句 -->
  <span
    :class="['inline-flex gap-0.5 leading-none', sizeClass]"
    role="img"
    :aria-label="`${rating} 顆星（滿分 5 顆）`"
  >
      <template v-for="n in 5" :key="n">
          <UIcon v-if="n <= rating" name="i-mdi-star" class="text-rating-star" aria-hidden="true" />
          <UIcon v-else name="i-mdi-star-outline" class="text-neutral-border" aria-hidden="true" />
      </template>
  </span>
</template>
