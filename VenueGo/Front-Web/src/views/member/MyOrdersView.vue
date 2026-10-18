<!--
  會員中心／訂單與付款：篩選、列表（電腦表格／手機卡片）、分頁、明細、付款
  跟「我的預約」的差別：這頁的重點是「錢付了沒、要付多少、什麼時候前要付」，排序是下單時間新到舊
  資料目前是假資料（api/orderApi.js），串接 API 時這個檔案不用改
  「付款」按鈕：3-A 先顯示按鈕和倒數，按下去提示「製作中」；3-D 再接上綠界付款流程（onPay）
  訂單編號跟「我的預約」同一個編號；點編號跳到我的預約並只查這一筆
  UFormField、UInput、USelect、UAlert、UButton 與 useOverlay、useToast 由 Nuxt UI 自動匯入
-->
<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import DatePickerInput from "@/components/DatePickerInput.vue";
import EmptyState from "@/components/EmptyState.vue";
import ListFilterPanel from "@/components/list/ListFilterPanel.vue";
import ListTable from "@/components/list/ListTable.vue";
import StatusBadge from "@/components/list/StatusBadge.vue";
import OrderDetailModal from "@/components/orders/OrderDetailModal.vue";
import { getMyOrders } from "@/api/orderApi";
import { useKeywordQuery } from "@/composables/useKeywordQuery";
import { useSiteClock } from "@/composables/useSiteClock";
import { ErrorCodes } from "@/constants/errorCodes";
import { PAYMENT_METHOD, PAYMENT_METHOD_LABEL, PAYMENT_METHOD_OPTIONS } from "@/constants/payment";
import { ALL, PAYMENT_STATUS, PAYMENT_STATUS_OPTIONS } from "@/constants/statuses";
import { useMemberStore } from "@/stores/member";
import { formatAmount, formatDate, formatDateTime, formatTimeRange } from "@/utils/format";
import { canPay, formatRemaining, showsDueAt } from "@/utils/order";

const PAGE_SIZE = 10;

const router = useRouter();
const memberStore = useMemberStore();
const toast = useToast();

// 網站的「現在時間」：每秒更新，所以「剩 N 分鐘」會自己倒數，時間到了「付款」按鈕自動消失
const { now } = useSiteClock();
const isPayable = (order) => canPay(order, now.value);
const remaining = (order) => formatRemaining(order.paymentStartDeadline, now.value);

// ── 篩選條件 ──
// filters 是「已套用」的條件；欄位正在編輯的草稿由 ListFilterPanel 管理，按查詢才會改到這裡
const DEFAULT_FILTERS = {
  keyword: "",
  date: null, // CalendarDate；null 表示不篩選日期
  paymentMethod: ALL,
  paymentStatus: ALL,
};
const filters = ref({ ...DEFAULT_FILTERS });
const page = ref(1);
// 關鍵字跟網址的 ?keyword= 同步：從「我的預約」點編號跳過來時，一進來就只查那一筆
useKeywordQuery(filters);

// 訂單編號連到「我的預約」，並用這個編號查詢
const reservationsLink = (orderNo) => ({ name: "reservations", query: { keyword: orderNo } });

// 有沒有套用篩選條件：決定沒有資料時顯示哪一種空狀態（同「我的預約」）
const hasActiveFilters = computed(() => {
  const f = filters.value;
  return (
    f.keyword.trim() !== "" || f.date !== null || f.paymentMethod !== ALL || f.paymentStatus !== ALL
  );
});

function resetFilters() {
  filters.value = { ...DEFAULT_FILTERS };
}

// ── 取資料 ──
const result = ref({ items: [], totalCount: 0 });
const loading = ref(false);
const errorMessage = ref("");
// 只採用最後一次查詢的結果（快速換頁時，舊的回應不會蓋掉新的）
let latestRequestId = 0;

async function load() {
  const requestId = ++latestRequestId;
  loading.value = true;
  errorMessage.value = "";
  try {
    const data = await getMyOrders({
      ...filters.value,
      date: filters.value.date?.toString() ?? null, // CalendarDate → "2026-10-20"
      page: page.value,
      pageSize: PAGE_SIZE,
    });
    if (requestId !== latestRequestId) return;
    result.value = data;
  } catch (err) {
    if (requestId !== latestRequestId) return;
    // 登入失效：登出並回登入頁，登入後回到這一頁（跟路由守衛的處理一致）
    if (err.errorCode === ErrorCodes.NotLoggedIn) {
      memberStore.logout();
      router.push({ name: "login", query: { redirect: "/member/orders" } });
      return;
    }
    errorMessage.value = err.message || "讀取訂單資料失敗，請稍後再試";
    result.value = { items: [], totalCount: 0 };
  } finally {
    if (requestId === latestRequestId) loading.value = false;
  }
}

// 條件改變 → 回到第 1 頁再查；已經在第 1 頁時 page 不會變，所以直接查
watch(filters, () => {
  if (page.value !== 1) page.value = 1;
  else load();
});
watch(page, load);
onMounted(load);

// ── 表格欄位（每一欄的顯示內容寫在下方 template 的 #xxx-cell）──
const columns = [
  {
    accessorKey: "orderNo",
    header: "訂單編號",
    meta: { class: { th: "text-center", td: "text-center" } },
  },
  {
    accessorKey: "venueName",
    header: "場地名稱",
    meta: { class: { th: "text-center", td: "text-center" } },
  },
  { id: "time", header: "使用時間", meta: { class: { th: "text-center", td: "text-center" } } },
  {
    accessorKey: "totalAmount",
    header: "金額",
    meta: { class: { th: "text-center", td: "text-center" } },
  },
  {
    accessorKey: "paymentMethod",
    header: "付款方式",
    meta: { class: { th: "text-center", td: "text-center" } },
  },
  {
    accessorKey: "paymentStatus",
    header: "付款狀態",
    meta: { class: { th: "text-center", td: "text-center" } },
  },
  {
    id: "deadline",
    header: "期限／付款時間",
    meta: { class: { th: "text-center", td: "text-center" } },
  },
  { id: "actions", header: "操作", meta: { class: { th: "text-center" } } },
];

// 「期限／付款時間」這一欄：未付款顯示期限、已付款顯示付款時間、其他（取消、逾期）顯示「—」
// 拆成「時間」和「說明」兩段：表格分兩行顯示（欄位比較窄），手機卡片接成一行
function deadlineParts(order) {
  if (order.paidAt) return { time: formatDateTime(order.paidAt), note: "付款" };
  if (!showsDueAt(order)) return null;
  const note = order.paymentMethod === PAYMENT_METHOD.ONSITE ? "前（櫃台付款）" : "前";
  return { time: formatDateTime(order.paymentDueAt), note };
}
function deadlineText(order) {
  const parts = deadlineParts(order);
  return parts ? `${parts.time} ${parts.note}` : "—";
}

// ── 明細、付款 ──
const detailModal = useOverlay().create(OrderDetailModal);

async function openDetail(order) {
  const choice = await detailModal.open({ order });
  if (choice === "pay") onPay(order);
}

// 表格、手機卡片、明細視窗三個入口都走這裡
// 3-A：綠界還沒串接，先提示；3-D 改成呼叫建立付款 API，帶客人到綠界付款頁
function onPay(order) {
  toast.add({
    title: "付款功能製作中",
    description: `訂單 ${order.orderNo}：線上付款（綠界）串接完成後即可付款。`,
    color: "info",
    icon: "i-mdi-information-outline",
  });
}
</script>

<template>
  <div class="flex flex-col gap-6">
    <!-- 標題＋篩選區（跟「我的預約」同樣的排版） -->
    <section class="rounded border border-neutral-border bg-neutral-surface p-4 md:p-6">
      <h1 class="mb-4 text-2xl font-bold text-neutral-text-primary md:mb-6">訂單與付款</h1>

      <ListFilterPanel v-model="filters" :defaults="DEFAULT_FILTERS">
        <template #default="{ draft }">
          <UFormField label="關鍵字">
            <UInput
              v-model="draft.keyword"
              icon="i-lucide-search"
              placeholder="訂單編號 / 場地名稱"
              class="w-full"
            />
          </UFormField>
          <UFormField label="使用日期">
            <DatePickerInput v-model="draft.date" />
          </UFormField>
          <UFormField label="付款方式">
            <USelect v-model="draft.paymentMethod" :items="PAYMENT_METHOD_OPTIONS" class="w-full" />
          </UFormField>
          <UFormField label="付款狀態">
            <USelect v-model="draft.paymentStatus" :items="PAYMENT_STATUS_OPTIONS" class="w-full" />
          </UFormField>
        </template>
      </ListFilterPanel>
    </section>

    <UAlert
      v-if="errorMessage"
      color="error"
      variant="soft"
      icon="i-mdi-alert-circle"
      :description="errorMessage"
      :actions="[{ label: '重新載入', color: 'error', variant: 'outline', onClick: load }]"
    />

    <ListTable
      v-else
      v-model:page="page"
      :rows="result.items"
      :columns="columns"
      :loading="loading"
      :total="result.totalCount"
      :page-size="PAGE_SIZE"
      row-key="orderId"
    >
      <template #empty>
        <EmptyState
          v-if="hasActiveFilters"
          icon="i-mdi-magnify-close"
          title="查無符合條件的訂單"
          description="請調整篩選條件後再查詢。"
          :actions="[
            { label: '清除篩選條件', color: 'primary', variant: 'outline', onClick: resetFilters },
          ]"
        />
        <EmptyState
          v-else
          icon="i-mdi-receipt-text-outline"
          title="目前沒有訂單"
          description="預約場地後，訂單與付款紀錄會顯示在這裡。"
          :actions="[
            { label: '立即預約場地', to: '/booking', trailingIcon: 'i-lucide-arrow-right' },
          ]"
        />
      </template>

      <!-- ── 電腦版表格（資料在 row.original 裡） ── -->
      <!-- 訂單編號：連到「我的預約」只查這一筆（樣式、寫法同「我的預約」的編號連結） -->
      <template #orderNo-cell="{ row }">
        <RouterLink
          :to="reservationsLink(row.original.orderNo)"
          class="rounded font-medium whitespace-nowrap text-brand-primary tabular-nums underline underline-offset-2 hover:no-underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
          :aria-label="`訂單 ${row.original.orderNo}，前往我的預約`"
        >
          {{ row.original.orderNo }}
        </RouterLink>
      </template>
      <!-- 使用時間、期限：分成兩行（上面日期、下面時間／說明），表格才放得進會員中心的寬度 -->
      <template #time-cell="{ row }">
        <span class="block whitespace-nowrap tabular-nums">
          {{ formatDate(row.original.bookingDate) }}
        </span>
        <span class="block whitespace-nowrap text-neutral-text-secondary tabular-nums">
          {{ formatTimeRange(row.original.startTime, row.original.endTime) }}
        </span>
      </template>
      <template #totalAmount-cell="{ row }">
        <span class="whitespace-nowrap tabular-nums">{{
          formatAmount(row.original.totalAmount)
        }}</span>
      </template>
      <template #paymentMethod-cell="{ row }">
        {{ PAYMENT_METHOD_LABEL[row.original.paymentMethod] }}
      </template>
      <template #paymentStatus-cell="{ row }">
        <StatusBadge :status="PAYMENT_STATUS[row.original.paymentStatus]" />
      </template>
      <template #deadline-cell="{ row }">
        <template v-if="deadlineParts(row.original)">
          <span class="block whitespace-nowrap tabular-nums">
            {{ deadlineParts(row.original).time }}
          </span>
          <span class="block whitespace-nowrap text-neutral-text-secondary">
            {{ deadlineParts(row.original).note }}
          </span>
        </template>
        <span v-else>—</span>
      </template>
      <template #actions-cell="{ row }">
        <div class="flex items-start gap-2">
          <!-- 每一列都有「查看」，讀屏軟體只唸「查看」分不出是哪一筆，所以補上 aria-label
               外框按鈕（outline）：跟「付款」一樣的大小與框線，顏色維持品牌藍
               :loading-auto="false"：全站按鈕預設會在 async 的 @click 執行期間轉圈（vite.config.js 的 loadingAuto），
               openDetail 會等明細視窗關閉才結束，不關掉的話視窗開著時「查看」一直轉圈 -->
          <UButton
            color="primary"
            variant="outline"
            size="sm"
            class="whitespace-nowrap"
            :aria-label="`查看訂單 ${row.original.orderNo} 明細`"
            :loading-auto="false"
            @click="openDetail(row.original)"
          >
            查看
          </UButton>
          <!-- 倒數放在按鈕下方：放在按鈕裡面會讓這一欄太寬 -->
          <div v-if="isPayable(row.original)" class="flex flex-col items-center gap-0.5">
            <UButton
              color="action"
              size="sm"
              class="whitespace-nowrap"
              :aria-label="`付款：訂單 ${row.original.orderNo}，${remaining(row.original)}`"
              @click="onPay(row.original)"
            >
              付款
            </UButton>
            <span class="text-xs whitespace-nowrap text-brand-action" aria-hidden="true">
              {{ remaining(row.original) }}
            </span>
          </div>
        </div>
      </template>

      <!-- ── 手機版卡片（資料直接是 row） ── -->
      <template #card="{ row }">
        <div class="flex flex-col gap-2 text-sm">
          <div class="flex items-center justify-between gap-2">
            <RouterLink
              :to="reservationsLink(row.orderNo)"
              class="rounded font-semibold text-brand-primary tabular-nums underline underline-offset-2 hover:no-underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
              :aria-label="`訂單 ${row.orderNo}，前往我的預約`"
            >
              {{ row.orderNo }}
            </RouterLink>
            <StatusBadge :status="PAYMENT_STATUS[row.paymentStatus]" />
          </div>
          <p class="text-base font-semibold text-neutral-text-primary">{{ row.venueName }}</p>
          <p class="text-neutral-text-secondary tabular-nums">
            {{ formatDate(row.bookingDate) }}
            {{ formatTimeRange(row.startTime, row.endTime) }} ·
            {{ formatAmount(row.totalAmount) }}
          </p>
          <p class="text-neutral-text-secondary tabular-nums">
            <!-- 沒有期限（處理中、取消、逾期）時只顯示付款方式，不顯示「・—」 -->
            {{ PAYMENT_METHOD_LABEL[row.paymentMethod]
            }}<template v-if="deadlineParts(row)">・{{ deadlineText(row) }}</template>
          </p>
          <div class="flex items-center justify-end gap-2">
            <UButton
              color="primary"
              variant="outline"
              :aria-label="`查看訂單 ${row.orderNo} 明細`"
              :loading-auto="false"
              @click="openDetail(row)"
            >
              查看
            </UButton>
            <UButton
              v-if="isPayable(row)"
              color="action"
              :aria-label="`付款：訂單 ${row.orderNo}，${remaining(row)}`"
              @click="onPay(row)"
            >
              付款
              <span class="text-xs font-normal opacity-90">{{ remaining(row) }}</span>
            </UButton>
          </div>
        </div>
      </template>
    </ListTable>
  </div>
</template>
