<script setup>
// 時段快速篩選：全部時段／上午／下午／晚上
// v-model：TIME_PERIODS 的 key（"all"、"morning"…）
// UIcon 由 Nuxt UI 自動匯入
import { TIME_PERIODS } from "@/constants/booking";

const model = defineModel({ type: String, required: true });
</script>

<template>
  <!-- role="group" + aria-pressed：讀屏軟體會唸出「按鈕，已按下」，知道目前選哪個 -->
  <div role="group" aria-label="時段篩選" class="flex gap-2 overflow-x-auto">
    <button
      v-for="period in TIME_PERIODS"
      :key="period.key"
      type="button"
      :aria-pressed="model === period.key"
      class="flex h-9 shrink-0 cursor-pointer items-center gap-1.5 rounded-full border px-3 text-sm transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
      :class="
        model === period.key
          ? 'border-brand-primary bg-brand-primary/8 font-semibold text-brand-primary'
          : 'border-neutral-border bg-neutral-surface text-neutral-text-secondary hover:border-brand-primary hover:text-brand-primary'
      "
      @click="model = period.key"
    >
      <UIcon :name="period.icon" class="size-4" />
      {{ period.label }}
      <span v-if="period.hint" class="hidden text-xs font-normal sm:inline">{{ period.hint }}</span>
    </button>
  </div>
</template>
