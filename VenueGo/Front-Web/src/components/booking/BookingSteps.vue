<script setup>
// 預約頁上方的步驟條：① 選擇時段 → ② 填寫資料 → ③ 預約完成
// current：目前在第幾步（1 開始）；之前的步驟打勾、目前的步驟藍色、之後的灰色
// UIcon 由 Nuxt UI 自動匯入
defineProps({
  current: { type: Number, required: true },
});

const STEPS = ["選擇時段", "填寫資料", "預約完成"];
</script>

<template>
  <!-- ol：步驟有先後順序；aria-current 讓讀屏軟體唸出「目前步驟」 -->
  <ol class="flex items-center gap-2 text-sm md:gap-4">
    <li
      v-for="(label, index) in STEPS"
      :key="label"
      class="flex items-center gap-2 md:gap-4"
      :class="index < STEPS.length - 1 ? 'flex-1' : ''"
      :aria-current="index + 1 === current ? 'step' : undefined"
    >
      <span class="flex shrink-0 items-center gap-2">
        <span
          class="flex size-7 items-center justify-center rounded-full text-xs font-semibold"
          :class="
            index + 1 < current
              ? 'bg-brand-primary text-white'
              : index + 1 === current
                ? 'bg-brand-primary text-white ring-4 ring-brand-primary/15'
                : 'border border-neutral-border bg-neutral-surface text-neutral-text-secondary'
          "
        >
          <UIcon v-if="index + 1 < current" name="i-mdi-check" class="size-4" />
          <template v-else>{{ index + 1 }}</template>
        </span>
        <!-- 手機只顯示目前步驟的文字，避免擠在一起 -->
        <span
          :class="[
            index + 1 === current
              ? 'font-semibold text-brand-primary'
              : 'hidden text-neutral-text-secondary sm:inline',
          ]"
        >
          {{ label }}
        </span>
      </span>
      <!-- 步驟之間的線 -->
      <span
        v-if="index < STEPS.length - 1"
        aria-hidden="true"
        class="h-px flex-1"
        :class="index + 1 < current ? 'bg-brand-primary' : 'bg-neutral-border'"
      />
    </li>
  </ol>
</template>
