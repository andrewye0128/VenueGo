<script setup>
// 列表頁的篩選區外框：欄位排列＋查詢／重置按鈕＋「按查詢才套用」
// 欄位由使用的頁面用 slot 放進來，所以「我的預約」「訂單與付款」可以各自放不同欄位
//
// 用法：
//   <ListFilterPanel v-model="filters" :defaults="DEFAULT_FILTERS">
//     <template #default="{ draft }">
//       <UFormField label="關鍵字"><UInput v-model="draft.keyword" /></UFormField>
//     </template>
//   </ListFilterPanel>
//
// filters：已套用的條件，頁面看到它改變才重新取資料
// draft：欄位正在編輯的草稿，按「查詢」（或在欄位裡按 Enter）才複製到 filters
// UButton 由 Nuxt UI 自動匯入
import { reactive, watch } from "vue";

const props = defineProps({
  // 按「重置」時要恢復成的條件
  defaults: { type: Object, required: true },
});

const filters = defineModel({ type: Object, required: true });

// 複製用 { ... }，不用 structuredClone：structuredClone 會把 CalendarDate 變成普通物件，
// 之後就不能呼叫 .toString()、.compare()；CalendarDate 本身不能被修改，共用同一個物件是安全的
const draft = reactive({ ...filters.value });

// 條件從外面被改掉時（例如之後從網址帶入），草稿也跟著更新
watch(filters, (value) => Object.assign(draft, value));

function search() {
  filters.value = { ...draft };
}

function reset() {
  Object.assign(draft, props.defaults);
  filters.value = { ...props.defaults };
}
</script>

<template>
  <!-- @container：欄位排列看「篩選區本身的寬度」，不是螢幕寬度，放在有側邊選單的頁面也會排得正確 -->
  <form
    class="@container rounded border border-neutral-border bg-neutral-surface p-4 md:p-6"
    @submit.prevent="search"
  >
    <!-- 窄：1 欄；中等：2 欄；寬：4 欄（設計稿） -->
    <div class="grid grid-cols-1 gap-4 @xl:grid-cols-2 @3xl:grid-cols-4">
      <slot :draft="draft" />
    </div>

    <!-- 手機：兩個按鈕各佔一半；寬的時候固定寬度靠左 -->
    <div class="mt-6 flex gap-3">
      <UButton
        type="submit"
        color="action"
        icon="i-lucide-search"
        class="flex-1 @xl:w-34 @xl:flex-none"
      >
        查詢
      </UButton>
      <UButton
        type="button"
        color="primary"
        variant="outline"
        class="flex-1 @xl:w-28 @xl:flex-none"
        @click="reset"
      >
        重置
      </UButton>
    </div>
  </form>
</template>
