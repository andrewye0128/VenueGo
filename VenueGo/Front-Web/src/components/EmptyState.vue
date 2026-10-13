<!--
  EmptyState.vue — 全站通用的「沒有資料」空狀態：插圖＋標題＋說明＋按鈕

  用法：
    <EmptyState
      icon="i-mdi-close"
      title="目前沒有預約紀錄"
      description="預約場地後，紀錄會顯示在這裡。"
      :actions="[{ label: '立即預約場地', to: '/booking', trailingIcon: 'i-lucide-arrow-right' }]"
    />

  參數：
    title        標題（必填）
    description  說明（選填，不傳就不顯示）
    icon         插圖中間卡片上的圖示，每頁可以換（預設空盒子）
                 圖示名稱要寫完整，Nuxt UI 打包時才掃描得到
    actions      按鈕陣列（選填），每一顆直接沿用 UButton 的參數：
                 label、to（連結）、onClick（點擊）、color、variant、icon、trailingIcon……
                 預設是橘色 action 按鈕；傳入的設定會蓋過預設值，
                 例如白底藍框：{ label: '清除篩選條件', color: 'primary', variant: 'outline', onClick: reset }
    decorated    要不要顯示雲朵、紙飛機裝飾（預設要）；空間小的地方（例如視窗裡）可以關掉

  slot：
    #illustration  換掉整張插圖
    #actions       換掉整個按鈕區

  role="status"：查詢後結果變成空的，讀屏軟體會自動唸出標題，視障使用者才知道查詢有結果
  UIcon、UButton 由 Nuxt UI 自動匯入
-->
<script setup>
defineProps({
  title: { type: String, required: true },
  description: { type: String, default: "" },
  icon: { type: String, default: "i-mdi-inbox-outline" },
  actions: { type: Array, default: () => [] },
  decorated: { type: Boolean, default: true },
});

// 按鈕預設樣式（設計稿：橘色、標準高度）
const ACTION_DEFAULTS = { color: "action", size: "md" };
</script>

<template>
  <div role="status" class="flex flex-col items-center px-4 py-10 text-center md:py-12">
    <!-- 插圖：純裝飾，讀屏軟體不用唸 -->
    <slot name="illustration">
      <div
        aria-hidden="true"
        class="relative shrink-0"
        :class="decorated ? 'h-36 w-60' : 'h-24 w-24'"
      >
        <!-- 裝飾：雲朵、紙飛機和它的虛線軌跡（座標以 240×144 的畫布計算） -->
        <svg
          v-if="decorated"
          viewBox="0 0 240 144"
          class="absolute inset-0 size-full"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
        >
          <!-- 雲朵：三個圓＋一個長方形組成平底的雲
               顏色不透明、整組再用 opacity 變淡：如果每個形狀各自半透明，
               重疊的地方會疊出比較深的顏色，雲看起來一塊一塊的 -->
          <g class="fill-neutral-border opacity-50">
            <circle cx="30" cy="96" r="8" />
            <circle cx="40" cy="91" r="11" />
            <circle cx="51" cy="96" r="8" />
            <rect x="30" y="96" width="21" height="8" />
          </g>
          <g class="fill-neutral-border opacity-40">
            <circle cx="56" cy="38" r="5" />
            <circle cx="62" cy="35" r="7" />
            <circle cx="69" cy="38" r="5" />
            <rect x="56" y="38" width="13" height="5" />
          </g>
          <g class="fill-neutral-border opacity-50">
            <circle cx="186" cy="110" r="7" />
            <circle cx="195" cy="106" r="10" />
            <circle cx="205" cy="110" r="7" />
            <rect x="186" y="110" width="19" height="7" />
          </g>

          <!-- 紙飛機的虛線軌跡：從卡片右側往右上飛 -->
          <path
            d="M166 78 C 180 76, 176 58, 186 54 S 196 46, 200 40"
            class="stroke-neutral-border"
            stroke-width="1.5"
            stroke-dasharray="3 4"
            stroke-linecap="round"
          />
          <!-- 紙飛機 -->
          <path
            d="M200 36 L224 24 L214 46 L208 39 Z"
            class="fill-neutral-surface stroke-neutral-text-secondary/60"
            stroke-width="1.5"
            stroke-linejoin="round"
          />
          <path
            d="M208 39 L224 24"
            class="stroke-neutral-text-secondary/60"
            stroke-width="1.5"
            stroke-linecap="round"
          />
        </svg>

        <!-- 中間的卡片：上方一條色帶＋兩個月曆環，中間放可替換的圖示 -->
        <div
          class="absolute top-1/2 left-1/2 size-20 -translate-x-1/2 -translate-y-1/2 rounded-xl border-2 border-neutral-border bg-neutral-surface"
        >
          <div class="h-4 rounded-t-[10px] bg-neutral-border/60" />
          <span
            class="absolute -top-1.5 left-5 h-3 w-1.5 rounded-full bg-neutral-text-secondary/40"
          />
          <span
            class="absolute -top-1.5 right-5 h-3 w-1.5 rounded-full bg-neutral-text-secondary/40"
          />
          <div class="flex h-[calc(100%-1rem)] items-center justify-center">
            <UIcon :name="icon" class="size-9 text-neutral-text-secondary/70" />
          </div>
        </div>
      </div>
    </slot>

    <p class="mt-4 text-lg font-bold text-neutral-text-primary">{{ title }}</p>
    <p v-if="description" class="mt-1 text-sm text-neutral-text-secondary">{{ description }}</p>

    <div v-if="actions.length || $slots.actions" class="mt-6 flex flex-wrap justify-center gap-3">
      <slot name="actions">
        <!-- 預設值在前、傳入的設定在後：傳入的會蓋過預設值 -->
        <UButton
          v-for="(action, index) in actions"
          :key="action.label ?? index"
          v-bind="{ ...ACTION_DEFAULTS, ...action }"
        />
      </slot>
    </div>
  </div>
</template>
