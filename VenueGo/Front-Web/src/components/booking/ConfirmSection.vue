<script setup>
// 填寫資料頁左邊大卡片裡的一個區塊：圖示＋標題、有框線的內容
// 排版參考後台確認頁的 <section class="border rounded p-3">
// 五個區塊（預約人、場地與時段、費用明細、付款與發票、確認事項）長得一樣，寫成元件只寫一次
//
// icon：標題前的圖示（完整名稱，例如 i-mdi-account）
// title：標題
// flush：內容不留內距（費用明細表格要貼齊框線）
// 插槽：預設＝內容；actions＝標題右邊的連結（例如「修改會員資料」）
// UIcon 由 Nuxt UI 自動匯入
import { useId } from "vue";

defineProps({
  icon: { type: String, required: true },
  title: { type: String, required: true },
  flush: { type: Boolean, default: false },
});

// 標題的 id：讓 section 用 aria-labelledby 指到標題，讀屏軟體會唸出區塊名稱
const headingId = useId();
</script>

<template>
  <section :aria-labelledby="headingId" class="rounded border border-neutral-border">
    <div
      class="flex items-center justify-between gap-2 px-4 pt-4"
      :class="flush ? 'border-b border-neutral-border pb-3' : ''"
    >
      <h3
        :id="headingId"
        class="flex items-center gap-2 text-base font-semibold text-neutral-text-primary"
      >
        <UIcon :name="icon" class="size-5 text-brand-primary" />
        {{ title }}
      </h3>
      <slot name="actions" />
    </div>
    <div :class="flush ? '' : 'p-4'">
      <slot />
    </div>
  </section>
</template>
