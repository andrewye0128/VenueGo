<script setup>
// 已選時段摘要
//   電腦（≥ 1024px）：右側欄，捲動時固定在畫面上（sticky）
//   手機、平板：有選時段時，固定在畫面最下方的一條；點左半邊從底部滑出抽屜，看完整明細
//
// summary：沒選是 null；有選是 { venueName, sportName, dateLabel, slots: [{ slotTime, unitPrice, isPeak }] }
// 明細內容在 BookingSummaryDetails.vue（右側欄、抽屜共用）
// UButton、UDrawer、UIcon 由 Nuxt UI 自動匯入
import { computed, nextTick, ref, watch } from "vue";
import BookingSummaryDetails from "@/components/booking/BookingSummaryDetails.vue";
import { formatAmount } from "@/utils/format";
import { slotEndTime } from "@/utils/slotSelection";

const props = defineProps({
  summary: { type: Object, default: null },
});

const emit = defineEmits(["clear"]);

// 底部那一條要用的簡短資訊
const total = computed(() => props.summary?.slots.reduce((sum, s) => sum + s.unitPrice, 0) ?? 0);
const hours = computed(() => props.summary?.slots.length ?? 0);
const timeRange = computed(() => {
  const slots = props.summary?.slots;
  if (!slots?.length) return "";
  return `${slots[0].slotTime}–${slotEndTime(slots[slots.length - 1].slotTime)}`;
});

// 階段 1：還不能填資料，「下一步」先停用（階段 2 再開放）
const NEXT_DISABLED_HINT = "填寫資料功能製作中";

// ── 手機抽屜 ──
const drawerOpen = ref(false);

// 沒有選擇了（清除、換日期、換運動），抽屜也沒有內容可以看，一起關掉
watch(
  () => props.summary,
  (value) => {
    if (!value) drawerOpen.value = false;
  },
);

// 焦點：UDrawer 底層的 vaul-vue 刻意取消了「打開時自動聚焦」，
// 不處理的話，鍵盤使用者打開抽屜後焦點還在背後的頁面，按 Tab 也會跑到抽屜外面
//   打開 → 焦點移到抽屜裡的明細（不是輸入框，手機不會跳出鍵盤）；
//          焦點進入抽屜後，抽屜內建的焦點鎖定才會生效
//   關閉 → 焦點還給底部那條的按鈕，回到原本的位置
const drawerBody = ref(null);
const barButton = ref(null);

watch(drawerOpen, async (open) => {
  await nextTick();
  // 抽屜的內容在下一個畫面才會出現，再等一個 frame 才找得到
  requestAnimationFrame(() => {
    if (open) drawerBody.value?.focus();
    else barButton.value?.focus();
  });
});

function clearAndClose() {
  drawerOpen.value = false;
  emit("clear");
}
</script>

<template>
  <!-- ── 電腦：右側欄 ── -->
  <div
    class="hidden rounded border border-neutral-border bg-neutral-surface p-4 lg:sticky lg:top-4 lg:block"
  >
    <h2 class="mb-3 font-semibold text-neutral-text-primary">已選時段</h2>

    <p v-if="!summary" class="text-sm text-neutral-text-secondary">
      尚未選擇時段，請先選擇場地，再點選時段。
    </p>
    <BookingSummaryDetails v-else :summary="summary" />

    <UButton color="action" block disabled class="mt-4">下一步：填寫資料</UButton>
    <p class="mt-1 text-center text-xs text-neutral-text-secondary">{{ NEXT_DISABLED_HINT }}</p>
    <UButton
      v-if="summary"
      color="primary"
      variant="ghost"
      block
      class="mt-2"
      @click="emit('clear')"
    >
      清除選擇
    </UButton>
  </div>

  <!-- ── 手機、平板：固定在最下方（有選時段才出現） ── -->
  <div
    v-if="summary"
    class="fixed inset-x-0 bottom-0 z-20 border-t border-neutral-border bg-neutral-surface px-4 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] lg:hidden"
  >
    <div class="mx-auto flex max-w-7xl items-center gap-2">
      <!-- 左半邊：點了打開抽屜看明細
           aria-haspopup、aria-expanded：讀屏軟體會唸出「會打開對話框」、目前有沒有打開 -->
      <button
        type="button"
        ref="barButton"
        aria-haspopup="dialog"
        :aria-expanded="drawerOpen"
        aria-label="查看已選時段明細"
        class="flex min-w-0 flex-1 cursor-pointer items-center gap-2 rounded py-1 text-start text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
        @click="drawerOpen = true"
      >
        <span class="min-w-0 flex-1">
          <span class="block truncate text-neutral-text-secondary">
            {{ summary.venueName }}・{{ timeRange }}
          </span>
          <span class="block font-semibold text-neutral-text-primary tabular-nums">
            {{ hours }} 小時・{{ formatAmount(total) }}
          </span>
        </span>
        <!-- 往上的箭頭：提示「點這裡可以展開」 -->
        <UIcon name="i-mdi-chevron-up" class="size-5 shrink-0 text-brand-primary" />
      </button>
      <UButton color="primary" variant="ghost" @click="emit('clear')">清除</UButton>
      <UButton color="action" disabled :title="NEXT_DISABLED_HINT">下一步</UButton>
    </div>
  </div>

  <!-- ── 手機抽屜：從底部滑出，往下滑、點背景、按 Esc、按右上角 ✕ 都可以關閉 ──
       只用 v-model:open 控制，所以不放觸發按鈕（觸發的是上面那條的左半邊） -->
  <UDrawer
    v-model:open="drawerOpen"
    title="已選時段"
    :close="{ 'aria-label': '關閉' }"
    :ui="{ container: 'mx-auto w-full max-w-lg' }"
  >
    <template #body>
      <!-- tabindex="-1"：讓程式可以把焦點移到這裡，但按 Tab 不會停在這一塊 -->
      <div ref="drawerBody" tabindex="-1" class="outline-none">
        <BookingSummaryDetails v-if="summary" :summary="summary" />
      </div>
    </template>
    <template #footer>
      <div class="grid grid-cols-2 gap-3">
        <UButton color="primary" variant="outline" block @click="clearAndClose">清除選擇</UButton>
        <UButton color="action" block disabled>下一步：填寫資料</UButton>
      </div>
      <p class="mt-1 text-center text-xs text-neutral-text-secondary">{{ NEXT_DISABLED_HINT }}</p>
    </template>
  </UDrawer>
</template>
