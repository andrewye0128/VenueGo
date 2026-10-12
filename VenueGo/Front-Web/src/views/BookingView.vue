<!--
  場地預約頁（階段 1：看時段、選時段；「下一步」先停用）
  網址：/booking?sportTypeId=2&date=2026-10-14
    沒有 sportTypeId → 全部運動（依運動分組顯示所有場地，不自動選場地）
    有 sportTypeId   → 只顯示這種運動，自動選中第一個有空位的場地
    沒有 date、或日期不在可預約範圍 → 今天
  運動、日期放在網址：重新整理、分享網址，畫面都一樣；換運動或日期用 router.replace，不會多出「上一頁」紀錄
  資料目前是假資料（api/bookingApi.js），串接 API 時這個檔案不用改
  useToast、UAlert、UButton 由 Nuxt UI 自動匯入
-->
<script setup>
import { computed, nextTick, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { parseDate } from "@internationalized/date";
import BookingDateBar from "@/components/booking/BookingDateBar.vue";
import BookingSteps from "@/components/booking/BookingSteps.vue";
import BookingSummary from "@/components/booking/BookingSummary.vue";
import SlotPicker from "@/components/booking/SlotPicker.vue";
import TimePeriodFilter from "@/components/booking/TimePeriodFilter.vue";
import VenueCards from "@/components/booking/VenueCards.vue";
import EmptyState from "@/components/EmptyState.vue";
import SportTypeSelect from "@/components/SportTypeSelect.vue";
import { getAvailability, getAvailabilitySummary } from "@/api/bookingApi";
import { useSiteClock } from "@/composables/useSiteClock";
import { useSportTypes } from "@/composables/useSportTypes";
import { DEFAULT_TIME_PERIOD, isInPeriod } from "@/constants/booking";
import {
  MAX_SLOTS_PER_RESERVATION,
  MEMBER_MAX_ADVANCE_DAYS,
  MEMBER_MIN_ADVANCE_DAYS,
} from "@/constants/reservationRules";
import { ErrorCodes } from "@/constants/errorCodes";
import { ALL_SPORTS } from "@/constants/sportTypes";
import { formatDate } from "@/utils/format";
import { isSelectableSlot, toggleSlot } from "@/utils/slotSelection";

const route = useRoute();
const router = useRouter();
const toast = useToast();
const { sportTypes, findSportType } = useSportTypes();

// ── 可預約的日期範圍：用網站時間（開發時跟著時光機），前後端對「今天」的認定才一致 ──
const { now, refresh: refreshClock } = useSiteClock();
const pad = (n) => String(n).padStart(2, "0");
// 回傳字串：時鐘每秒更新，但同一天的字串一樣，依賴它的計算不會每秒重算
const todayKey = computed(() => {
  const d = now.value;
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
});
const minDate = computed(() =>
  parseDate(todayKey.value).add({ days: MEMBER_MIN_ADVANCE_DAYS }).toString(),
);
const maxDate = computed(() =>
  parseDate(todayKey.value).add({ days: MEMBER_MAX_ADVANCE_DAYS }).toString(),
);

// ── 網址參數 → 目前的運動、日期 ──
const sportTypeId = computed(() => {
  const id = Number(route.query.sportTypeId);
  return Number.isInteger(id) && id > 0 ? id : null; // null = 全部運動
});
const date = computed(() => {
  const value = route.query.date;
  const valid =
    typeof value === "string" &&
    /^\d{4}-\d{2}-\d{2}$/.test(value) &&
    value >= minDate.value && // "YYYY-MM-DD" 可以直接比字串
    value <= maxDate.value;
  return valid ? value : minDate.value;
});

function updateQuery(patch) {
  // undefined 的參數 Vue Router 不會放進網址（例如全部運動就不帶 sportTypeId）
  router.replace({ query: { ...route.query, ...patch } });
}

// 網址參數不合法時改成正確的值（例如日期超過 30 天、運動類型不存在），網址才跟畫面一致
watch(
  [() => route.query.date, date],
  ([raw, valid]) => {
    if (raw !== undefined && raw !== valid) updateQuery({ date: valid });
  },
  { immediate: true },
);
watch([sportTypeId, sportTypes], ([id, list]) => {
  if (id !== null && list.length > 0 && !findSportType(id)) updateQuery({ sportTypeId: undefined });
});

// 給 SportTypeSelect、日期列用的 v-model（讀網址、寫網址）
const sportModel = computed({
  get: () => sportTypeId.value ?? ALL_SPORTS,
  set: (value) => updateQuery({ sportTypeId: value === ALL_SPORTS ? undefined : value }),
});
const dateModel = computed({
  get: () => date.value,
  set: (value) => updateQuery({ date: value }),
});

// 時段篩選（只影響畫面，不放進網址）
const period = ref(DEFAULT_TIME_PERIOD);

// 已選的時段：{ venueId, slotTimes: ["18:00", "19:00"] }，沒選是 null
// 要宣告在下面的 watch 之前：那個 watch 一開始就會執行，會用到它
const selection = ref(null);

// ── 讀取這一天的場地與時段 ──
const availability = ref(null);
const loading = ref(false);
const errorMessage = ref("");
let latestRequestId = 0; // 只採用最後一次的結果（快速切換日期時，舊的回應不會蓋掉新的）

async function loadAvailability() {
  const requestId = ++latestRequestId;
  loading.value = true;
  errorMessage.value = "";
  const requestedDate = date.value;
  try {
    const data = await getAvailability({ sportTypeId: sportTypeId.value, date: requestedDate });
    if (requestId !== latestRequestId) return;
    availability.value = data;
    autoSelectVenue();
  } catch (err) {
    if (requestId !== latestRequestId) return;

    // 日期不在可預約範圍：通常是午夜交界，前端的時鐘還沒換日、後端已經是隔天了
    // 先讓前端時鐘跟後端重新對時；對時後「今天」變了，date 會自動改成新的今天，
    // 上面 watch([sportTypeId, date]) 會自己重新讀取，這裡就不顯示錯誤
    if (err.errorCode === ErrorCodes.DateOutOfRange) {
      await refreshClock();
      if (date.value !== requestedDate) return;
    }

    errorMessage.value = err.message || "讀取場地時段失敗，請稍後再試";
    availability.value = null;
  } finally {
    if (requestId === latestRequestId) loading.value = false;
  }
}

// 換運動或日期：清空已選的時段，重新讀取
watch(
  [sportTypeId, date],
  () => {
    selection.value = null;
    loadAvailability();
  },
  { immediate: true },
);

// ── 日期列每天剩幾格 ──
const counts = ref({});
const visibleDates = ref([]);
let latestSummaryId = 0;

async function loadSummary() {
  if (visibleDates.value.length === 0) return;
  const requestId = ++latestSummaryId;
  try {
    const list = await getAvailabilitySummary({
      sportTypeId: sportTypeId.value,
      dates: visibleDates.value,
      period: period.value,
    });
    if (requestId !== latestSummaryId) return;
    counts.value = Object.fromEntries(list.map((d) => [d.date, d.availableCount]));
  } catch {
    // 剩餘格數只是參考資訊，讀不到就不顯示數字，不影響預約
    if (requestId === latestSummaryId) counts.value = {};
  }
}

function onRangeChange(dates) {
  visibleDates.value = dates;
}
watch([visibleDates, sportTypeId, period], () => {
  counts.value = {};
  loadSummary();
});

// ── 場地卡片：依運動分組，並依「時段篩選」算出剩幾格、最早幾點 ──
// 「剩幾格」只算能選的格子（可預約而且有價格），跟時段按鈕用同一條規則
const isAvailable = isSelectableSlot;

const venueGroups = computed(() => {
  const venues = availability.value?.venues ?? [];
  // 依運動類型清單的順序分組；清單還沒讀到時，至少用場地本身的 sportTypeId 分組
  const order = sportTypes.value.map((s) => s.sportTypeId);
  const ids = [...new Set(venues.map((v) => v.sportTypeId))].sort(
    (a, b) => order.indexOf(a) - order.indexOf(b),
  );
  return ids.map((id) => {
    const sport = findSportType(id);
    return {
      sportTypeId: id,
      sportName: sport?.sportName ?? "",
      icon: sport?.icon ?? "i-mdi-run",
      venues: venues
        .filter((v) => v.sportTypeId === id)
        .map((v) => {
          const open = v.slots.filter(
            (s) => isAvailable(s) && isInPeriod(s.slotTime, period.value),
          );
          return { ...v, availableCount: open.length, earliest: open[0]?.slotTime ?? null };
        }),
    };
  });
});

const allVenues = computed(() => venueGroups.value.flatMap((g) => g.venues));
// 這天所有場地都沒有時段 = 公休
const isClosedDay = computed(
  () => allVenues.value.length > 0 && allVenues.value.every((v) => v.slots.length === 0),
);

// ── 選中的場地 ──
const selectedVenueId = ref(null);
const selectedVenue = computed(() =>
  allVenues.value.find((v) => v.venueId === selectedVenueId.value),
);

// 讀完資料後決定選哪個場地：
//   原本選的場地還在 → 保留（例如只是換日期）
//   指定了運動 → 自動選第一個有空位的場地（從首頁查詢進來的人，直接看到時段）
//   全部運動 → 不自動選，讓使用者自己挑
function autoSelectVenue() {
  if (selectedVenue.value) return;
  selectedVenueId.value =
    sportTypeId.value === null
      ? null
      : (allVenues.value.find((v) => v.availableCount > 0)?.venueId ?? null);
}

// 使用者點了場地卡片：時段在卡片下方，手機上可能在畫面外，捲過去
const slotsSection = ref(null);
async function onVenuePick() {
  await nextTick();
  const el = slotsSection.value;
  if (el && el.getBoundingClientRect().top > window.innerHeight * 0.6) {
    el.scrollIntoView({ behavior: "smooth", block: "start" });
  }
}

const visibleSlots = computed(
  () => selectedVenue.value?.slots.filter((s) => isInPeriod(s.slotTime, period.value)) ?? [],
);

// ── 選時段（規則在 utils/slotSelection.js） ──
const selectedTimes = computed(() =>
  selection.value?.venueId === selectedVenueId.value ? selection.value.slotTimes : [],
);

function onSlotSelect(slotTime) {
  const result = toggleSlot(
    selection.value,
    selectedVenueId.value,
    slotTime,
    MAX_SLOTS_PER_RESERVATION,
  );
  selection.value = result.selection;
  if (result.message) {
    toast.add({ title: result.message, color: "warning", icon: "i-mdi-alert-outline" });
  }
}

// 摘要：選了別的場地的時段時，摘要仍顯示「已選的那個場地」
const WEEKDAYS = ["日", "一", "二", "三", "四", "五", "六"];
const dateLabel = computed(() => {
  const d = parseDate(date.value);
  return `${formatDate(date.value)}（${WEEKDAYS[new Date(d.year, d.month - 1, d.day).getDay()]}）`;
});
const summary = computed(() => {
  if (!selection.value) return null;
  const venue = allVenues.value.find((v) => v.venueId === selection.value.venueId);
  if (!venue) return null;
  return {
    venueName: venue.venueName,
    sportName: findSportType(venue.sportTypeId)?.sportName ?? "",
    dateLabel: dateLabel.value,
    slots: venue.slots.filter((s) => selection.value.slotTimes.includes(s.slotTime)),
  };
});

// 公休日的「看下一天」
const nextDay = computed(() => parseDate(date.value).add({ days: 1 }).toString());
</script>

<template>
  <!-- 手機有選時段時，底部會固定一條摘要，加大下方留白避免擋住內容 -->
  <main
    class="mx-auto max-w-7xl px-4 py-6 md:px-6 md:py-10"
    :class="summary ? 'pb-28 lg:pb-10' : ''"
  >
    <h1 class="text-2xl font-bold text-neutral-text-primary">場地預約</h1>
    <div class="mt-4">
      <BookingSteps :current="1" />
    </div>

    <!-- ── 篩選：運動、時段、日期 ── -->
    <section
      aria-label="篩選條件"
      class="mt-6 flex flex-col gap-4 rounded border border-neutral-border bg-neutral-surface p-4 md:p-6"
    >
      <div class="flex flex-col gap-4 md:flex-row md:items-end">
        <div class="md:w-64">
          <SportTypeSelect v-model="sportModel" include-all />
        </div>
        <div class="min-w-0">
          <p class="mb-1.5 text-sm font-medium text-neutral-text-primary">時段</p>
          <TimePeriodFilter v-model="period" />
        </div>
      </div>
      <div>
        <p class="mb-1.5 text-sm font-medium text-neutral-text-primary">日期</p>
        <BookingDateBar
          v-model="dateModel"
          :min-date="minDate"
          :max-date="maxDate"
          :counts="counts"
          @range="onRangeChange"
        />
      </div>
    </section>

    <div class="mt-6 lg:grid lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start lg:gap-6">
      <div class="flex flex-col gap-6">
        <UAlert
          v-if="errorMessage"
          color="error"
          variant="soft"
          icon="i-mdi-alert-circle"
          :description="errorMessage"
          :actions="[
            { label: '重新載入', color: 'error', variant: 'outline', onClick: loadAvailability },
          ]"
        />

        <p
          v-else-if="loading && !availability"
          class="rounded border border-neutral-border bg-neutral-surface py-16 text-center text-sm text-neutral-text-secondary"
        >
          載入中…
        </p>

        <template v-else-if="availability">
          <!-- 這項運動沒有場地 -->
          <div
            v-if="allVenues.length === 0"
            class="rounded border border-neutral-border bg-neutral-surface"
          >
            <EmptyState
              icon="i-mdi-stadium-outline"
              title="目前沒有可預約的場地"
              description="請改選其他運動類型。"
            />
          </div>

          <!-- 公休日 -->
          <div
            v-else-if="isClosedDay"
            class="rounded border border-neutral-border bg-neutral-surface"
          >
            <EmptyState
              icon="i-mdi-calendar-remove-outline"
              title="這天沒有開放預約"
              description="場館公休，請選擇其他日期。"
              :actions="
                nextDay <= maxDate
                  ? [
                      {
                        label: '看下一天',
                        trailingIcon: 'i-lucide-arrow-right',
                        onClick: () => (dateModel = nextDay),
                      },
                    ]
                  : []
              "
            />
          </div>

          <template v-else>
            <!-- ── 場地 ── -->
            <section
              aria-labelledby="venue-heading"
              class="rounded border border-neutral-border bg-neutral-surface p-4 md:p-6"
              :class="loading ? 'opacity-60' : ''"
            >
              <h2 id="venue-heading" class="mb-3 font-semibold text-neutral-text-primary">
                選擇場地
              </h2>
              <VenueCards
                v-model="selectedVenueId"
                :groups="venueGroups"
                :show-headings="sportTypeId === null"
                @update:model-value="onVenuePick"
              />
            </section>

            <!-- ── 時段 ── -->
            <section
              ref="slotsSection"
              aria-labelledby="slot-heading"
              class="scroll-mt-4 rounded border border-neutral-border bg-neutral-surface mb-2 p-4 md:p-6"
              :class="loading ? 'opacity-60' : ''"
            >
              <template v-if="selectedVenue">
                <div class="mb-3 flex flex-wrap items-baseline justify-between gap-2">
                  <h2 id="slot-heading" class="font-semibold text-neutral-text-primary">
                    {{ selectedVenue.venueName }} 的時段
                  </h2>
                  <p class="text-xs text-neutral-text-secondary">
                    一次最多選 {{ MAX_SLOTS_PER_RESERVATION }} 小時，時段需連續
                  </p>
                </div>
                <SlotPicker
                  v-if="visibleSlots.length"
                  :slots="visibleSlots"
                  :selected-times="selectedTimes"
                  @select="onSlotSelect"
                />
                <p v-else class="py-6 text-center text-sm text-neutral-text-secondary">
                  這個場地在目前的時段篩選中沒有開放，請改選其他時段。
                </p>
              </template>
              <p
                v-else
                id="slot-heading"
                class="py-6 text-center text-sm text-neutral-text-secondary"
              >
                請選擇場地，查看可預約的時段。
              </p>
            </section>
          </template>
        </template>
      </div>

      <!-- ── 已選摘要（電腦右側、手機底部） ── -->
      <aside aria-label="已選時段" class="mt-6 lg:mt-0">
        <BookingSummary :summary="summary" @clear="selection = null" />
      </aside>
    </div>
  </main>
</template>
