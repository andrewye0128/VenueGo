<script setup>
// 預約頁的日期列：一次顯示 7 天，每天下面顯示「剩幾格」，點一下切換日期
//   ◀ ▶：一次跳 7 天；右邊月曆按鈕：直接跳到某一天
//
// v-model：選中的日期 "2026-10-14"
// minDate、maxDate：可預約的範圍（今天～30 天後）
// counts：{ "2026-10-14": 12, ... }，還沒讀到的日期不顯示數字
// 顯示範圍改變時 emit("range", ["2026-10-14", ...7 天])，由頁面去讀這幾天的剩餘格數
// UButton、UPopover、UCalendar 由 Nuxt UI 自動匯入
import { computed, ref, watch } from "vue";
import { parseDate } from "@internationalized/date";
import { DATE_BAR_DAYS } from "@/constants/booking";

const model = defineModel({ type: String, required: true });

const props = defineProps({
  minDate: { type: String, required: true },
  maxDate: { type: String, required: true },
  counts: { type: Object, default: () => ({}) },
});

const emit = defineEmits(["range"]);

const WEEKDAYS = ["日", "一", "二", "三", "四", "五", "六"];
const min = computed(() => parseDate(props.minDate));
const max = computed(() => parseDate(props.maxDate));

// 一開始顯示的 7 天從哪一天開始：
//   選的日期在「今天起 7 天內」→ 從今天開始（例如從首頁帶 3 天後進來，也看得到今天、明天）
//   超過 7 天 → 從選的日期開始
function startFor(selected) {
  const firstWeekEnd = min.value.add({ days: DATE_BAR_DAYS - 1 });
  if (selected.compare(min.value) < 0 || selected.compare(firstWeekEnd) <= 0) return min.value;
  return selected;
}

const windowStart = ref(startFor(parseDate(model.value)));

// 之後選中的日期跑出目前範圍時（例如從月曆跳到下個月），再把範圍移過去
watch(model, (value) => {
  const selected = parseDate(value);
  const end = windowStart.value.add({ days: DATE_BAR_DAYS - 1 });
  if (selected.compare(windowStart.value) < 0 || selected.compare(end) > 0) {
    windowStart.value = startFor(selected);
  }
});

const days = computed(() =>
  Array.from({ length: DATE_BAR_DAYS }, (_, i) => {
    const date = windowStart.value.add({ days: i });
    const key = date.toString();
    return {
      key,
      label: `${date.month}/${date.day}`,
      weekday: WEEKDAYS[new Date(date.year, date.month - 1, date.day).getDay()],
      isToday: key === props.minDate, // 最早可預約的日期就是今天（可以訂當天）
      outOfRange: date.compare(min.value) < 0 || date.compare(max.value) > 0,
      count: props.counts[key],
    };
  }),
);

// 只看「顯示哪幾天」，組成一個字串來比較：
// 不能直接 watch days，因為剩餘格數（counts）更新時 days 也會變，
// 會一直通知頁面重新讀取 → 讀完又更新 counts → 又通知，變成無限循環
const visibleKeys = computed(() =>
  days.value
    .filter((d) => !d.outOfRange)
    .map((d) => d.key)
    .join(","),
);
watch(visibleKeys, (keys) => emit("range", keys ? keys.split(",") : []), { immediate: true });

const canPrev = computed(() => windowStart.value.compare(min.value) > 0);
const canNext = computed(
  () => windowStart.value.add({ days: DATE_BAR_DAYS }).compare(max.value) <= 0,
);

function shift(direction) {
  let next = windowStart.value.add({ days: DATE_BAR_DAYS * direction });
  if (next.compare(min.value) < 0) next = min.value;
  windowStart.value = next;
}

// ── 月曆 ──
const calendarOpen = ref(false);
const calendarValue = computed(() => parseDate(model.value));
function pickFromCalendar(date) {
  if (!date) return;
  model.value = date.toString();
  calendarOpen.value = false;
}
</script>

<template>
  <div class="flex items-stretch gap-2">
    <UButton
      color="primary"
      variant="outline"
      icon="i-mdi-chevron-left"
      square
      aria-label="前 7 天"
      :disabled="!canPrev"
      class="self-center"
      @click="shift(-1)"
    />

    <!-- 手機放不下 7 天時可以左右滑動 -->
    <ul class="flex flex-1 gap-1 overflow-x-auto md:grid md:grid-cols-7 md:overflow-visible">
      <li v-for="day in days" :key="day.key" class="min-w-16 flex-1 md:min-w-0">
        <button
          type="button"
          :disabled="day.outOfRange"
          :aria-pressed="day.key === model"
          :aria-label="`${day.label}（${day.weekday}）${day.outOfRange ? '，不開放預約' : day.count === undefined ? '' : day.count === 0 ? '，已額滿' : `，剩 ${day.count} 格`}`"
          class="flex w-full cursor-pointer flex-col items-center rounded border px-1 py-2 text-sm transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary disabled:cursor-not-allowed disabled:opacity-40"
          :class="
            day.key === model
              ? 'border-brand-primary bg-brand-primary text-white'
              : 'border-neutral-border bg-neutral-surface text-neutral-text-primary hover:border-brand-primary'
          "
          @click="model = day.key"
        >
          <span
            class="text-xs"
            :class="day.key === model ? 'text-white/85' : 'text-neutral-text-secondary'"
          >
            {{ day.isToday ? "今天" : `週${day.weekday}` }}
          </span>
          <span class="font-semibold tabular-nums">{{ day.label }}</span>
          <!-- 剩餘格數：讀取中先佔位，避免高度跳動 -->
          <span
            class="mt-0.5 h-4 text-xs tabular-nums"
            :class="
              day.key === model
                ? 'text-white/85'
                : day.count === 0
                  ? 'text-semantic-error'
                  : 'text-neutral-text-secondary'
            "
          >
            <template v-if="day.outOfRange">—</template>
            <template v-else-if="day.count === 0">已額滿</template>
            <template v-else-if="day.count !== undefined">剩 {{ day.count }}</template>
          </span>
        </button>
      </li>
    </ul>

    <UButton
      color="primary"
      variant="outline"
      icon="i-mdi-chevron-right"
      square
      aria-label="後 7 天"
      :disabled="!canNext"
      class="self-center"
      @click="shift(1)"
    />

    <UPopover v-model:open="calendarOpen">
      <UButton
        color="primary"
        variant="outline"
        icon="i-mdi-calendar-month-outline"
        square
        aria-label="開啟月曆選擇日期"
        class="self-center"
      />
      <template #content>
        <UCalendar
          :model-value="calendarValue"
          :min-value="min"
          :max-value="max"
          locale="zh-TW"
          class="p-2"
          @update:model-value="pickFromCalendar"
        />
      </template>
    </UPopover>
  </div>
</template>
