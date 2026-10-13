<!--
  會員中心／我的預約：篩選、列表（電腦表格／手機卡片）、分頁、明細、取消
  資料來自後端 GET /api/reservations（api/reservationApi.js），會員身分由登入的 JWT 決定
  UFormField、UInput、USelect、UAlert 與 useOverlay、useToast 由 Nuxt UI 自動匯入
-->
<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import CancelConfirmModal from "@/components/CancelConfirmModal.vue";
import DatePickerInput from "@/components/DatePickerInput.vue";
import EmptyState from "@/components/EmptyState.vue";
import ListFilterPanel from "@/components/list/ListFilterPanel.vue";
import ListTable from "@/components/list/ListTable.vue";
import StatusBadge from "@/components/list/StatusBadge.vue";
import ReservationDetailModal from "@/components/reservations/ReservationDetailModal.vue";
import { cancelMyReservation, getMyReservations } from "@/api/reservationApi";
import { useSiteClock } from "@/composables/useSiteClock";
import { ErrorCodes } from "@/constants/errorCodes";
import { useMemberStore } from "@/stores/member";
import {
  ALL,
  PAYMENT_STATUS,
  PAYMENT_STATUS_OPTIONS,
  RESERVATION_STATUS,
  RESERVATION_STATUS_OPTIONS,
} from "@/constants/statuses";
import { formatAmount, formatDate, formatTimeRange } from "@/utils/format";
import { canCancel, formatPersonCount, formatReservationNo } from "@/utils/reservation";

const PAGE_SIZE = 10;

const router = useRouter();
const memberStore = useMemberStore();

// 網站的「現在時間」：開發時跟著後端的時光機走，正式版本就是電腦時間（說明見 useSiteClock.js）
// 判斷「還沒到開始時間、可以取消」要用它，前後端對「現在」的認定才會一致
const { now } = useSiteClock();
const isCancellable = (reservation) => canCancel(reservation, now.value);

// ── 篩選條件 ──
// filters 是「已套用」的條件；欄位正在編輯的草稿由 ListFilterPanel 管理，按查詢才會改到這裡
const DEFAULT_FILTERS = {
  keyword: "",
  date: null, // CalendarDate；null 表示不篩選日期
  reservationStatus: ALL,
  paymentStatus: ALL,
};
const filters = ref({ ...DEFAULT_FILTERS });
const page = ref(1);

// 有沒有套用篩選條件：決定沒有資料時顯示哪一種空狀態
//   沒有篩選 → 「目前沒有預約紀錄」（真的沒預約過）
//   有篩選   → 「查無符合條件的預約」（有預約，只是條件太嚴格；避免會員以為預約不見了）
const hasActiveFilters = computed(() => {
  const f = filters.value;
  return (
    f.keyword.trim() !== "" ||
    f.date !== null ||
    f.reservationStatus !== ALL ||
    f.paymentStatus !== ALL
  );
});

// 清除篩選條件：改回預設值，ListFilterPanel 的欄位會跟著更新，列表也會重新查詢
function resetFilters() {
  filters.value = { ...DEFAULT_FILTERS };
}

// ── 取資料 ──
const result = ref({ items: [], totalCount: 0 });
const loading = ref(false);
const errorMessage = ref("");

// 每次查詢編一個號碼，只採用最後一次查詢的結果：
// 例如快速連點第 2 頁、第 3 頁，第 2 頁的回應比較晚回來時，不會蓋掉第 3 頁
let latestRequestId = 0;

async function load() {
  const requestId = ++latestRequestId;
  loading.value = true;
  errorMessage.value = "";
  try {
    const data = await getMyReservations({
      ...filters.value,
      date: filters.value.date?.toString() ?? null, // CalendarDate → "2026-10-08"
      page: page.value,
      pageSize: PAGE_SIZE,
    });
    if (requestId !== latestRequestId) return;
    result.value = data;
    // 測試空資料時用：
    // result.value = { items: [], totalCount: 0 };
  } catch (err) {
    if (requestId !== latestRequestId) return;
    // 登入失效（例如 token 過期）：留在這頁也什麼都做不了，登出並回登入頁
    // 跟路由守衛「進入會員中心時 token 無效」的處理一致
    if (err.errorCode === ErrorCodes.NotLoggedIn) {
      memberStore.logout();
      router.push({ name: "login" });
      return;
    }
    // http.js 已經把錯誤整理成中文的 err.message
    errorMessage.value = err.message || "讀取預約資料失敗，請稍後再試";
    result.value = { items: [], totalCount: 0 };
  } finally {
    if (requestId === latestRequestId) loading.value = false;
  }
}

// 條件改變 → 回到第 1 頁再查（停在第 3 頁時篩選，結果可能只剩 1 頁，畫面會是空的）
// 已經在第 1 頁時 page 不會變，所以直接查
watch(filters, () => {
  if (page.value !== 1) page.value = 1;
  else load();
});
watch(page, load);
onMounted(load);

// ── 表格欄位 ──
// accessorKey：對應資料的欄位名稱；沒有對應欄位的（使用時段、操作）用 id
// 每一欄的顯示內容寫在下方 template 的 #xxx-cell
const columns = [
  { accessorKey: "reservationId", header: "預約編號" },
  { accessorKey: "venueName", header: "場地名稱" },
  { accessorKey: "bookingDate", header: "預約日期" },
  { id: "time", header: "使用時段" },
  { accessorKey: "personCount", header: "預約人數" },
  { accessorKey: "reservationStatus", header: "預約狀態" },
  { accessorKey: "paymentStatus", header: "付款狀態" },
  { id: "actions", header: "操作" },
];

// ── 明細、取消 ──
const overlay = useOverlay();
const detailModal = overlay.create(ReservationDetailModal);
const cancelModal = overlay.create(CancelConfirmModal);
const toast = useToast();

async function openDetail(reservation) {
  // 能不能取消由頁面決定後傳進去：判斷要用網站時間，明細視窗不必自己處理
  const choice = await detailModal.open({
    reservation,
    cancellable: isCancellable(reservation),
  });
  if (choice === "cancel") openCancel(reservation);
}

// 表格、手機卡片、明細視窗三個入口都走這裡
async function openCancel(reservation) {
  const reservationNo = formatReservationNo(reservation.reservationId);
  const done = await cancelModal.open({
    title: "取消預約",
    description: "確定要取消以下預約嗎？",
    details: [
      { label: "預約編號", value: reservationNo },
      { label: "場地名稱", value: reservation.venueName },
      {
        label: "使用時間",
        value: `${formatDate(reservation.bookingDate)} ${formatTimeRange(reservation.startTime, reservation.endTime)}`,
      },
    ],
    // 已付款（2）：提醒退款。退款流程還在討論，文字先跟後台的取消訊息一致
    notice:
      reservation.paymentStatus === 2
        ? `此預約已付款 ${formatAmount(reservation.totalAmount)}，取消後將辦理退款，請至櫃檯辦理退款手續。`
        : "",
    action: (reason) => cancelMyReservation(reservation.reservationId, reason),
  });

  if (done) {
    toast.add({
      title: `已取消預約 ${reservationNo}`,
      color: "success",
      icon: "i-mdi-check-circle",
    });
    load();
  }
}
</script>

<template>
  <div class="flex flex-col gap-6">
    <!-- 標題＋篩選區（設計稿：同一張白色卡片，篩選區自己再一圈淡灰框） -->
    <section class="rounded border border-neutral-border bg-neutral-surface p-4 md:p-6">
      <h1 class="mb-4 text-2xl font-bold text-neutral-text-primary md:mb-6">我的預約</h1>

      <ListFilterPanel v-model="filters" :defaults="DEFAULT_FILTERS">
        <template #default="{ draft }">
          <UFormField label="關鍵字">
            <UInput
              v-model="draft.keyword"
              icon="i-lucide-search"
              placeholder="場地名稱 / 預約編號"
              class="w-full"
            />
          </UFormField>
          <UFormField label="預約日期">
            <DatePickerInput v-model="draft.date" />
          </UFormField>
          <UFormField label="預約狀態">
            <USelect
              v-model="draft.reservationStatus"
              :items="RESERVATION_STATUS_OPTIONS"
              class="w-full"
            />
          </UFormField>
          <UFormField label="付款狀態">
            <USelect v-model="draft.paymentStatus" :items="PAYMENT_STATUS_OPTIONS" class="w-full" />
          </UFormField>
        </template>
      </ListFilterPanel>
    </section>

    <!-- 取資料失敗 -->
    <UAlert
      v-if="errorMessage"
      color="error"
      variant="soft"
      icon="i-mdi-alert-circle"
      :description="errorMessage"
      :actions="[{ label: '重新載入', color: 'error', variant: 'outline', onClick: load }]"
    />

    <!-- 取資料失敗時只顯示上面的錯誤訊息，不顯示列表（不然會同時出現「目前沒有預約紀錄」，讓人誤會） -->
    <ListTable
      v-else
      v-model:page="page"
      :rows="result.items"
      :columns="columns"
      :loading="loading"
      :total="result.totalCount"
      :page-size="PAGE_SIZE"
      row-key="reservationId"
    >
      <!-- ── 沒有資料：依「有沒有篩選」顯示不同的空狀態 ── -->
      <template #empty>
        <EmptyState
          v-if="hasActiveFilters"
          icon="i-mdi-magnify-close"
          title="查無符合條件的預約"
          description="請調整篩選條件後再查詢。"
          :actions="[
            {
              label: '清除篩選條件',
              color: 'primary',
              variant: 'outline',
              onClick: resetFilters,
            },
          ]"
        />
        <EmptyState
          v-else
          icon="i-mdi-close"
          title="目前沒有預約紀錄"
          description="預約場地後，紀錄會顯示在這裡。"
          :actions="[
            { label: '立即預約場地', to: '/booking', trailingIcon: 'i-lucide-arrow-right' },
          ]"
        />
      </template>

      <!-- ── 電腦版表格：每一欄的顯示內容（資料在 row.original 裡） ── -->
      <template #reservationId-cell="{ row }">
        {{ formatReservationNo(row.original.reservationId) }}
      </template>
      <template #bookingDate-cell="{ row }">
        {{ formatDate(row.original.bookingDate) }}
      </template>
      <template #time-cell="{ row }">
        {{ formatTimeRange(row.original.startTime, row.original.endTime) }}
      </template>
      <!-- 人數、付款狀態：預約沒有訂單或付款紀錄時是 null，顯示「—」 -->
      <template #personCount-cell="{ row }">
        {{ row.original.personCount ?? "—" }}
      </template>
      <template #reservationStatus-cell="{ row }">
        <StatusBadge :status="RESERVATION_STATUS[row.original.reservationStatus]" />
      </template>
      <template #paymentStatus-cell="{ row }">
        <StatusBadge
          v-if="row.original.paymentStatus != null"
          :status="PAYMENT_STATUS[row.original.paymentStatus]"
        />
        <span v-else>—</span>
      </template>
      <template #actions-cell="{ row }">
        <div class="flex gap-4">
          <!-- 每一列都有「查看」，讀屏軟體只唸「查看」會分不出是哪一筆，所以補上 aria-label -->
          <button
            type="button"
            class="cursor-pointer rounded font-medium text-brand-primary underline underline-offset-4 hover:no-underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
            :aria-label="`查看 ${formatReservationNo(row.original.reservationId)} 預約明細`"
            @click="openDetail(row.original)"
          >
            查看
          </button>
          <button
            v-if="isCancellable(row.original)"
            type="button"
            class="cursor-pointer rounded font-medium text-brand-action underline underline-offset-4 hover:no-underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
            :aria-label="`取消 ${formatReservationNo(row.original.reservationId)} 預約`"
            @click="openCancel(row.original)"
          >
            取消
          </button>
        </div>
      </template>

      <!-- ── 手機版卡片（資料直接是 row） ── -->
      <template #card="{ row }">
        <div class="flex flex-col gap-2 text-sm">
          <div class="flex items-center justify-between gap-2">
            <span class="font-semibold text-neutral-text-primary">
              {{ formatReservationNo(row.reservationId) }}
            </span>
            <StatusBadge :status="RESERVATION_STATUS[row.reservationStatus]" />
          </div>
          <p class="text-base font-semibold text-neutral-text-primary">{{ row.venueName }}</p>
          <p class="text-neutral-text-secondary">
            {{ formatDate(row.bookingDate) }}
            {{ formatTimeRange(row.startTime, row.endTime) }} ·
            {{ formatPersonCount(row.personCount) }}
          </p>
          <div class="flex items-center justify-between gap-2">
            <!-- 沒有付款紀錄時不顯示付款標籤，用空的 span 佔位，讓「查看／取消」維持靠右 -->
            <StatusBadge
              v-if="row.paymentStatus != null"
              :status="PAYMENT_STATUS[row.paymentStatus]"
            />
            <span v-else />
            <div class="flex gap-4">
              <button
                type="button"
                class="cursor-pointer rounded px-1 py-2 font-medium text-brand-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-brand-primary"
                :aria-label="`查看 ${formatReservationNo(row.reservationId)} 預約明細`"
                @click="openDetail(row)"
              >
                查看
              </button>
              <button
                v-if="isCancellable(row)"
                type="button"
                class="cursor-pointer rounded px-1 py-2 font-medium text-brand-action underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-brand-primary"
                :aria-label="`取消 ${formatReservationNo(row.reservationId)} 預約`"
                @click="openCancel(row)"
              >
                取消
              </button>
            </div>
          </div>
        </div>
      </template>
    </ListTable>
  </div>
</template>
