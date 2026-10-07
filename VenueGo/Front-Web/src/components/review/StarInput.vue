<!--
  StarInput.vue — 星等輸入
  用法（放在 UFormField 裡，錯誤訊息由 UFormField 顯示）：
    <UFormField label="評分" name="starRating" required>
      <StarInput v-model="state.starRating" />
    </UFormField>

  底下是 5 個真正的 radio（視覺上藏起來），所以鍵盤的方向鍵、表單的無障礙語意都還在。
  「滑過預覽」只在真的有滑鼠的裝置啟用：觸控裝置點一下時瀏覽器會補發 mouseenter，
  之後不一定有 mouseleave，預覽會卡在舊的那顆。
  觸控裝置每顆星加 padding，點擊範圍約 46×46px（WCAG 建議 44px）。

  設計規範沒有星等輸入元件，所以這是專案自己的元件（跟 StarView 用同一組顏色：rating-star／neutral-border）。
-->
<script setup>
import { ref, computed, useId } from "vue";

const model = defineModel({ type: Number, default: null });

// 同一頁放兩個星等元件時，radio 的 name 與 id 才不會撞在一起
const uid = useId();

const hover = ref(null);
const lit = computed(() => hover.value ?? model.value ?? 0);

const canHover = typeof window !== "undefined" && window.matchMedia("(hover: hover)").matches;

function onEnter(n) {
  if (canHover) hover.value = n;
}
</script>

<template>
  <div
    role="radiogroup"
    aria-label="評分"
    class="inline-flex gap-1 text-3xl leading-none pointer-coarse:gap-0"
    @mouseleave="hover = null"
  >
    <!-- 每顆星自己包一層 span：Tailwind 的 peer 只看同一層的兄弟，第 3 顆取得焦點時只有第 3 顆的外框會亮 -->
    <span v-for="n in 5" :key="n" class="relative">
      <input
        :id="`${uid}-${n}`"
        v-model="model"
        type="radio"
        :name="`${uid}-star`"
        :value="n"
        class="peer sr-only"
      />
      <label
        :for="`${uid}-${n}`"
        class="block cursor-pointer rounded peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-brand-primary pointer-coarse:p-2"
        @mouseenter="onEnter(n)"
      >
        <UIcon
          v-if="n <= lit"
          name="i-mdi-star"
          class="block text-rating-star"
          aria-hidden="true"
        />
        <UIcon
          v-else
          name="i-mdi-star-outline"
          class="block text-neutral-border"
          aria-hidden="true"
        />
        <span class="sr-only">{{ n }} 星</span>
      </label>
    </span>
  </div>
</template>
