<!--
  SectionMoreLink.vue — 首頁區塊標題右邊的「看全部…」連結（顧客評價、場地介紹、最新消息共用）

  用法：
    <SectionMoreLink :to="{ name: 'review-index' }">看全部評價</SectionMoreLink>

  樣式只寫在這裡，三個區塊才會一致（原本是評論組 HomeReviewCarousel 的「看全部評價」的樣式）
  外層的標題列請用 items-baseline：連結的字才會跟左邊標題的字對齊同一條底線
-->
<script setup>
import { RouterLink } from "vue-router";
import IconChevronRightBold from "@/components/icons/IconChevronRightBold.vue";

defineProps({
  // 連到哪裡，同 RouterLink 的 to：字串路徑（"/news"）或 { name: "..." } 都可以
  to: { type: [String, Object], required: true },
});
</script>

<template>
  <RouterLink
    :to="to"
    class="inline-flex items-center gap-1 text-base font-semibold text-brand-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
  >
    <slot />
    <!-- 箭頭用專案自己的粗箭頭，跟輪播的左右箭頭同一組 -->
    <IconChevronRightBold class="size-4 self-center" aria-hidden="true" />
  </RouterLink>
</template>
