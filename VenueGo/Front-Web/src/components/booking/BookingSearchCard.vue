<script setup>
// 首頁的查詢卡片：上方是「看起來像分頁的連結列」，下方是「場地預約」的查詢表單
// 按「查詢可預約場地」→ 跳到 /booking?sport=basketball&date=2026-10-05
// UForm、UFormField、UInputDate、UPopover、UCalendar、UButton、UIcon 由 Nuxt UI 自動匯入
import { reactive, ref } from "vue";
import { RouterLink, useRouter } from "vue-router";
import { z } from "zod";
import { today, getLocalTimeZone } from "@internationalized/date";
import SportTypeSelect from "@/components/SportTypeSelect.vue";
import { mainNavItems } from "@/constants/navigation";
import { MEMBER_MIN_ADVANCE_DAYS, MEMBER_MAX_ADVANCE_DAYS } from "@/constants/reservationRules";

const router = useRouter();

// ── 分頁連結列 ──
// 只有「場地預約」是表單，其他 3 個是連結，網址取自 navigation.js（跟 Header 共用，改網址不會漏改）
// 圖示名稱要寫完整，Nuxt UI 打包時才掃描得到
const linkTo = (label) => mainNavItems.find((item) => item.label === label)?.to;
const tabLinks = [
  { label: "收費標準", to: linkTo("收費標準"), icon: "i-mdi-cash-multiple" },
  { label: "場館資訊", to: linkTo("場館資訊"), icon: "i-mdi-office-building-outline" },
  { label: "最新消息", to: linkTo("最新消息"), icon: "i-mdi-newspaper-variant-outline" },
];

// ── 可預約的日期範圍 ──
// 用電腦所在的時區算「今天」：不能用 new Date().toISOString()，它是國際標準時間，
// 台灣早上 8 點以前會算成前一天
const todayDate = today(getLocalTimeZone());
const minDate = todayDate.add({ days: MEMBER_MIN_ADVANCE_DAYS });
const maxDate = todayDate.add({ days: MEMBER_MAX_ADVANCE_DAYS });

// 畫面上顯示 2026/10/05；網址用 2026-10-05（CalendarDate.toString() 的格式）
const pad = (n) => String(n).padStart(2, "0");
const formatDate = (date) => `${date.year}/${pad(date.month)}/${pad(date.day)}`;

const schema = z.object({
  sport: z.string().min(1, "請選擇運動類型"),
  date: z.custom((value) => value && value.compare(minDate) >= 0 && value.compare(maxDate) <= 0, {
    error: `請選擇 ${formatDate(minDate)} ～ ${formatDate(maxDate)} 之間的日期`,
  }),
});

// 預設選最早可以預約的那天
const state = reactive({
  sport: "",
  date: minDate,
});

// 選完月曆上的日期就關閉月曆
const calendarOpen = ref(false);

function onSubmit(event) {
  router.push({
    name: "booking",
    query: {
      // 暫時用運動代碼；之後 SportTypeSelect 改成接 API 時，改成 sportTypeId
      sport: event.data.sport,
      date: event.data.date.toString(),
    },
  });
}
</script>

<template>
  <div class="rounded border border-neutral-border bg-neutral-surface">
    <!-- 分頁連結列：放不下時（手機）可以左右滑動；圖示只在電腦版顯示
         底部灰線用 inset shadow 畫在 nav 內部（不是立體陰影）：
         如果用 border-b + 分頁 -mb-px 讓藍線蓋住灰線，分頁會超出 1px，
         加上 overflow-x-auto 後上下也會變成可捲動，Windows 上會出現小小的直向捲軸 -->
    <nav
      aria-label="快速連結"
      class="flex overflow-x-auto px-2 shadow-[inset_0_-1px_0_var(--color-neutral-border)] md:px-4"
    >
      <span
        aria-current="page"
        class="flex shrink-0 items-center gap-2 border-b-2 border-brand-primary px-4 py-3 text-sm font-semibold text-brand-primary"
      >
        <UIcon name="i-mdi-calendar-month" class="hidden size-5 xl:block" />
        場地預約
      </span>
      <RouterLink
        v-for="tab in tabLinks"
        :key="tab.label"
        :to="tab.to"
        class="flex shrink-0 items-center gap-2 border-b-2 border-transparent px-4 py-3 text-sm text-neutral-text-secondary transition-colors hover:text-brand-primary"
      >
        <UIcon :name="tab.icon" class="hidden size-5 xl:block" />
        {{ tab.label }}
      </RouterLink>
    </nav>

    <!-- 表單：手機直排、平板運動＋日期一排（按鈕在下一排）、電腦 3 欄一排 -->
    <UForm
      :schema="schema"
      :state="state"
      class="grid grid-cols-1 gap-4 p-4 md:grid-cols-2 md:p-6 xl:grid-cols-[1fr_1fr_auto] xl:items-start"
      @submit="onSubmit"
    >
      <SportTypeSelect v-model="state.sport" name="sport" include-all required />

      <UFormField label="日期" name="date" required>
        <!-- locale="en-ZA"（南非英文）：Reka 顯示日期時不補 0，zh-TW 會顯示 2026/10/5；
             en-ZA 的日期格式剛好是 2026/10/05，所以借用它。只影響輸入框，月曆仍用 zh-TW -->
        <UInputDate
          v-model="state.date"
          :min-value="minDate"
          :max-value="maxDate"
          locale="en-ZA"
          icon="i-mdi-calendar-blank-outline"
          class="w-full"
        >
          <template #trailing>
            <UPopover v-model:open="calendarOpen">
              <UButton
                color="neutral"
                variant="link"
                size="sm"
                icon="i-mdi-calendar-month-outline"
                aria-label="開啟月曆"
              />
              <template #content>
                <UCalendar
                  v-model="state.date"
                  :min-value="minDate"
                  :max-value="maxDate"
                  locale="zh-TW"
                  class="p-2"
                  @update:model-value="calendarOpen = false"
                />
              </template>
            </UPopover>
          </template>
        </UInputDate>
      </UFormField>

      <!-- xl:mt-6：電腦版跟輸入框對齊（Label 高度 20px + 間距 4px） -->
      <UButton
        type="submit"
        color="action"
        icon="i-lucide-search"
        block
        class="md:col-span-2 xl:col-span-1 xl:mt-6 xl:w-auto xl:px-10"
      >
        查詢可預約場地
      </UButton>
    </UForm>
  </div>
</template>
