<script setup>
// 運動類型下拉選單：組員只要 <SportTypeSelect v-model="sport" /> 就能使用
// UFormField、USelect 由 Nuxt UI 自動匯入
import { computed } from "vue";
import { sportTypes, ALL_SPORTS } from "@/constants/sportTypes";

const model = defineModel({ type: String, default: "" });

const props = defineProps({
  label: { type: String, default: "運動類型" },
  placeholder: { type: String, default: "請選擇運動類型" },
  // true 時清單最上面多一個「全部運動」（搜尋、篩選用）
  includeAll: { type: Boolean, default: false },
  // 要停用的運動，例如 ['swimming']；停用的選項會加上「（暫停開放）」
  disabledValues: { type: Array, default: () => [] },
  disabled: { type: Boolean, default: false },
  error: { type: String, default: "" },
  // 放在 UForm 裡時，要跟 schema 的欄位名稱一樣，驗證錯誤才會顯示在這個選單下方
  name: { type: String, default: undefined },
  // Label 旁邊顯示必填的 * 號
  required: { type: Boolean, default: false },
});

// 不鎖整頁捲動，避免捲軸消失/出現造成 Header、Footer 右側閃動
const contentOptions = { bodyLock: false };

const items = computed(() => {
  const sports = sportTypes.map((sport) => {
    const isDisabled = props.disabledValues.includes(sport.value);
    return {
      ...sport,
      // Design System 3.4：狀態不能只靠顏色，停用時加上文字說明
      label: isDisabled ? `${sport.label}（暫停開放）` : sport.label,
      disabled: isDisabled,
    };
  });

  return props.includeAll
    ? [{ label: "全部運動", value: ALL_SPORTS, icon: "i-mdi-view-grid" }, ...sports]
    : sports;
});

// USelect 不會自動在選單框顯示選中項目的 icon，這裡幫組員查出來
const selectedIcon = computed(() => items.value.find((item) => item.value === model.value)?.icon);
</script>

<template>
  <!-- error 沒值時要傳 undefined，不能傳 false：false 代表「永遠不顯示錯誤」，UForm 的驗證錯誤會被藏起來 -->
  <UFormField :label="label" :name="name" :required="required" :error="error || undefined">
    <USelect
      v-model="model"
      :items="items"
      :icon="selectedIcon"
      :content="contentOptions"
      :placeholder="placeholder"
      :disabled="disabled"
      class="w-full"
    />
  </UFormField>
</template>
