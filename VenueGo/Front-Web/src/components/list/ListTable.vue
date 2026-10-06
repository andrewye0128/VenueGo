<script setup>
// 列表頁的列表：寬的時候顯示表格（UTable）、窄的時候顯示卡片；含載入中、查無資料、分頁
// 表格欄位和卡片內容由使用的頁面決定，所以「我的預約」「訂單與付款」可以共用
//
// 用法：
//   <ListTable :rows="items" :columns="columns" :loading="loading"
//              :total="totalCount" v-model:page="page" row-key="reservationId">
//     <template #reservationStatus-cell="{ row }"> {{ row.original.xxx }} </template>  ← 表格的某一欄
//     <template #card="{ row }"> {{ row.xxx }} </template>                             ← 手機版一張卡片
//   </ListTable>
//
// ⚠️ 兩種 slot 拿到的 row 不一樣：
//   xxx-cell：UTable 規定的格式，資料在 row.original 裡
//   card：直接就是那一筆資料
// UTable、UPagination 由 Nuxt UI 自動匯入
import { computed, ref, useSlots } from "vue";

const props = defineProps({
  rows: { type: Array, default: () => [] },
  // UTable 的欄位定義：[{ accessorKey: "venueName", header: "場地名稱" }, ...]
  columns: { type: Array, required: true },
  loading: { type: Boolean, default: false },
  // 符合條件的總筆數（不是這一頁的筆數），用來算總共幾頁
  total: { type: Number, default: 0 },
  pageSize: { type: Number, default: 10 },
  // 每筆資料不會重複的欄位名稱，卡片列表的 key 用
  rowKey: { type: String, required: true },
  emptyText: { type: String, default: "查無資料" },
});

const page = defineModel("page", { type: Number, default: 1 });

// 頁面寫的 xxx-cell、xxx-header slot 原封不動轉交給 UTable；card 是卡片用的，不轉交
// 用函式不用 computed：useSlots() 不是響應式的，放進 computed 不會跟著更新
const slots = useSlots();
const tableSlotNames = () =>
  Object.keys(slots).filter((name) => name.endsWith("-cell") || name.endsWith("-header"));

const showPagination = computed(() => props.total > props.pageSize);

// 換頁後，如果列表頂端已經捲出畫面，就捲回列表頂端（不然手機上按了下一頁，畫面還停在最底下）
const rootRef = ref(null);
function changePage(value) {
  page.value = value;
  if (rootRef.value?.getBoundingClientRect().top < 0) {
    rootRef.value.scrollIntoView({ block: "start" });
  }
}

// 表格樣式：表頭淡灰底、每列之間灰線，對應設計稿
const tableUi = {
  thead: "bg-brand-background",
  th: "px-4 py-3 text-sm font-semibold text-neutral-text-secondary whitespace-nowrap",
  td: "px-4 py-3 text-sm text-neutral-text-primary",
  separator: "bg-neutral-border",
  empty: "py-10 text-center text-sm text-neutral-text-secondary",
};

// 分頁：只要 ‹ ›，不要「第一頁、最後一頁」按鈕（設計稿）
const paginationUi = {
  root: "flex justify-center",
  first: "hidden",
  last: "hidden",
};
</script>

<template>
  <!-- @container：表格或卡片看「列表本身的寬度」決定，不是螢幕寬度 -->
  <div
    ref="rootRef"
    class="@container scroll-mt-4 rounded border border-neutral-border bg-neutral-surface"
  >
    <!-- 寬（≥ 832px）：表格 -->
    <UTable
      :data="rows"
      :columns="columns"
      :loading="loading"
      :empty="emptyText"
      :ui="tableUi"
      class="hidden @[52rem]:block"
    >
      <template v-for="name in tableSlotNames()" :key="name" #[name]="scope">
        <slot :name="name" v-bind="scope" />
      </template>
    </UTable>

    <!-- 窄（< 832px）：卡片 -->
    <div class="@[52rem]:hidden">
      <p v-if="loading" class="py-10 text-center text-sm text-neutral-text-secondary">載入中…</p>
      <p
        v-else-if="rows.length === 0"
        class="py-10 text-center text-sm text-neutral-text-secondary"
      >
        {{ emptyText }}
      </p>
      <ul v-else class="divide-y divide-neutral-border">
        <li v-for="row in rows" :key="row[rowKey]" class="p-4">
          <slot name="card" :row="row" />
        </li>
      </ul>
    </div>

    <!-- 分頁：只有 1 頁時不顯示 -->
    <div v-if="showPagination" class="border-t border-neutral-border p-4">
      <UPagination
        :page="page"
        :total="total"
        :items-per-page="pageSize"
        :sibling-count="1"
        color="primary"
        variant="outline"
        active-color="primary"
        active-variant="solid"
        :ui="paginationUi"
        @update:page="changePage"
      />
    </div>
  </div>
</template>
