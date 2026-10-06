<script setup>
// 選擇日期欄位：外觀跟 UInput 一樣，點一下打開月曆；沒有值時顯示「選擇日期」，有值時顯示 2026/10/05
// 用法：<DatePickerInput v-model="draft.date" />（值是 CalendarDate，沒選是 null；送 API 時用 .toString() 轉成 2026-10-05）
// 跟 UInputDate 的差別：UInputDate 沒有值時只能顯示 yyyy/mm/dd，無法顯示「選擇日期」這種提示字
// UPopover、UCalendar、UIcon 由 Nuxt UI 自動匯入
import { ref } from "vue";
import { useFormField } from "@nuxt/ui/composables";
import { formatDate } from "@/utils/format";

const props = defineProps({
  placeholder: { type: String, default: "選擇日期" },
  // 可選的日期範圍（CalendarDate）；不傳就不限制
  minValue: { type: Object, default: undefined },
  maxValue: { type: Object, default: undefined },
});

const model = defineModel({ type: Object, default: null });

// 放在 UFormField 裡時，取得它產生的 id，讓欄位名稱（label）連到這個按鈕：
// 點欄位名稱會聚焦到按鈕，讀屏軟體也會唸出「預約日期」
// emitFormChange：放在 UForm 裡時通知它「值改了」，需要驗證時才會觸發
const { id, ariaAttrs, emitFormChange } = useFormField(props);

const open = ref(false);

function select(date) {
  model.value = date;
  open.value = false;
  emitFormChange();
}

function clear() {
  model.value = null;
  emitFormChange();
}
</script>

<template>
  <!-- 清除按鈕不能放在選日期的按鈕裡面（HTML 規定按鈕裡不能再放按鈕），所以疊在外層的右側 -->
  <div class="relative">
    <UPopover v-model:open="open">
      <!-- 用 <button>：鍵盤 Tab 到這裡按 Enter／空白鍵就能打開月曆 -->
      <button
        :id="id"
        type="button"
        v-bind="ariaAttrs"
        class="flex h-10 w-full cursor-pointer items-center gap-2 rounded bg-neutral-surface ps-3 pe-10 text-start text-sm ring ring-neutral-border transition-colors ring-inset hover:ring-brand-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary data-[state=open]:ring-brand-primary"
      >
        <UIcon
          name="i-mdi-calendar-blank-outline"
          class="size-5 shrink-0 text-neutral-text-secondary"
        />
        <span
          class="truncate"
          :class="model ? 'text-neutral-text-primary' : 'text-neutral-text-secondary'"
        >
          {{ model ? formatDate(model) : placeholder }}
        </span>
      </button>

      <template #content>
        <UCalendar
          :model-value="model"
          :min-value="minValue"
          :max-value="maxValue"
          locale="zh-TW"
          class="p-2"
          @update:model-value="select"
        />
      </template>
    </UPopover>

    <!-- 有值：清除按鈕；沒有值：月曆小圖示（只是裝飾，點擊會穿透到下面的按鈕） -->
    <button
      v-if="model"
      type="button"
      aria-label="清除日期"
      class="absolute end-2 top-1/2 flex -translate-y-1/2 cursor-pointer rounded p-1 text-neutral-text-secondary transition-colors hover:bg-brand-primary/8 hover:text-brand-primary focus-visible:outline-2 focus-visible:outline-brand-primary"
      @click="clear"
    >
      <UIcon name="i-mdi-close" class="size-4" />
    </button>
    <UIcon
      v-else
      name="i-mdi-calendar-month-outline"
      class="pointer-events-none absolute end-3 top-1/2 size-5 -translate-y-1/2 text-neutral-text-secondary"
    />
  </div>
</template>
