<script setup>
// 運動類型下拉選單：組員只要 <SportTypeSelect v-model="sportTypeId" /> 就能使用
// 選項從後端讀取（useSportTypes），v-model 的值是 sportTypeId（數字）；
// 選「全部運動」時是 ALL_SPORTS（"all"），還沒選是 ""
// UFormField、USelect 由 Nuxt UI 自動匯入
import { computed } from "vue";
import { ALL_SPORTS } from "@/constants/sportTypes";
import { useSportTypes } from "@/composables/useSportTypes";

const model = defineModel({ type: [Number, String], default: "" });

const props = defineProps({
  label: { type: String, default: "運動類型" },
  placeholder: { type: String, default: "請選擇運動類型" },
  // true 時清單最上面多一個「全部運動」（搜尋、篩選用）
  includeAll: { type: Boolean, default: false },
  // 要停用的運動 sportTypeId，例如 [5]；停用的選項會加上「（暫停開放）」
  disabledValues: { type: Array, default: () => [] },
  disabled: { type: Boolean, default: false },
  error: { type: String, default: "" },
  // 放在 UForm 裡時，要跟 schema 的欄位名稱一樣，驗證錯誤才會顯示在這個選單下方
  name: { type: String, default: undefined },
  // Label 旁邊顯示必填的 * 號
  required: { type: Boolean, default: false },
});

const { sportTypes, loading, error: loadError } = useSportTypes();

// 不鎖整頁捲動，避免捲軸消失/出現造成 Header、Footer 右側閃動
const contentOptions = { bodyLock: false };

const items = computed(() => {
  const sports = sportTypes.value.map((sport) => {
    const isDisabled = props.disabledValues.includes(sport.sportTypeId);
    return {
      // Design System 3.4：狀態不能只靠顏色，停用時加上文字說明
      label: isDisabled ? `${sport.sportName}（暫停開放）` : sport.sportName,
      value: sport.sportTypeId,
      icon: sport.icon,
      disabled: isDisabled,
    };
  });

  return props.includeAll
    ? [{ label: "全部運動", value: ALL_SPORTS, icon: "i-mdi-view-grid" }, ...sports]
    : sports;
});

// USelect 不會自動在選單框顯示選中項目的 icon，這裡幫組員查出來
const selectedIcon = computed(() => items.value.find((item) => item.value === model.value)?.icon);

// 讀取運動類型失敗時，選單下方顯示原因（頁面傳入的驗證錯誤優先）
const errorText = computed(() => props.error || loadError.value || undefined);
</script>

<template>
  <!-- error 沒值時要傳 undefined，不能傳 false：false 代表「永遠不顯示錯誤」，UForm 的驗證錯誤會被藏起來 -->
  <UFormField :label="label" :name="name" :required="required" :error="errorText">
    <USelect
      v-model="model"
      :items="items"
      :icon="selectedIcon"
      :content="contentOptions"
      :placeholder="loading ? '載入中…' : placeholder"
      :loading="loading"
      :disabled="disabled"
      class="w-full"
    />
  </UFormField>
</template>
