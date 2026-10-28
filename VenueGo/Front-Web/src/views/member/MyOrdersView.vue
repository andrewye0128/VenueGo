<!--
  會員中心／訂單與付款：篩選、列表（電腦表格／手機卡片）、分頁、明細、付款
  跟「我的預約」的差別：這頁的重點是「錢付了沒、要付多少、什麼時候前要付」，排序是下單時間新到舊
  資料來自 [GET] /api/orders（api/orderApi.js）；明細視窗另外讀 /api/orders/{orderId}
  付款期限到了會自動重新讀取（useDueRefresh），後端排程改成已逾期後不用重新整理
  「付款」按鈕：向後端開一張綠界付款單（[POST] /api/orders/{orderId}/payment/start），再整頁跳到綠界刷卡頁（onPay）
  付完錢綠界會把客人帶回這一頁（?keyword=訂單編號&from=ecpay），跳出付款結果視窗（PaymentResultModal）
  訂單編號跟「我的預約」同一個編號；點編號跳到我的預約並只查這一筆
  UFormField、UInput、USelect、UAlert、UButton 與 useOverlay、useToast 由 Nuxt UI 自動匯入
-->
<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import DatePickerInput from "@/components/DatePickerInput.vue";
import EmptyState from "@/components/EmptyState.vue";
import ListFilterPanel from "@/components/list/ListFilterPanel.vue";
import ListTable from "@/components/list/ListTable.vue";
import StatusBadge from "@/components/list/StatusBadge.vue";
import OrderDetailModal from "@/components/orders/OrderDetailModal.vue";
import PaymentResultModal from "@/components/orders/PaymentResultModal.vue";
import { getMyOrders, startPayment } from "@/api/orderApi";
import { useDueRefresh } from "@/composables/useDueRefresh";
import { useKeywordQuery } from "@/composables/useKeywordQuery";
import { useSiteClock } from "@/composables/useSiteClock";
import { ErrorCodes } from "@/constants/errorCodes";
import { PAYMENT_METHOD, PAYMENT_METHOD_LABEL, PAYMENT_METHOD_OPTIONS } from "@/constants/payment";
import { ALL, PAYMENT_STATUS, PAYMENT_STATUS_OPTIONS } from "@/constants/statuses";
import { useMemberStore } from "@/stores/member";
import { submitToEcpay } from "@/utils/ecpayRedirect";
import { formatAmount, formatDate, formatDateTime, formatTimeRange } from "@/utils/format";
import { canPay, formatRemaining, showsDueAt } from "@/utils/order";
import { RETURN_FROM_ECPAY } from "@/utils/paymentResult";

const PAGE_SIZE = 10;

const route = useRoute();
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

/**
 * 讀取這一頁的訂單
 * @param {object} [options]
 * @param {boolean} [options.silent] 自動重新讀取（useDueRefresh）用：不顯示讀取中、
 *   失敗時保留原本的資料也不顯示錯誤（使用者沒按任何東西，突然跳出錯誤會很奇怪，下次再試就好）
 */
async function load({ silent = false } = {}) {
  const requestId = ++latestRequestId;
  if (!silent) {
    loading.value = true;
    errorMessage.value = "";
  }
  try {
    const data = await getMyOrders({
      ...filters.value,
      date: filters.value.date?.toString() ?? null, // CalendarDate → "2026-10-20"
      page: page.value,
      pageSize: PAGE_SIZE,
    });
    if (requestId !== latestRequestId) return;
    // 這一頁已經超過最後一頁（例如第 2 頁唯一的一筆逾期後，不再符合「未付款」的篩選）
    // → 跳到最後一頁，不然畫面會顯示「查無資料」，但其實前一頁還有。改 page 會自動再查一次
    const lastPage = Math.max(Math.ceil(data.totalCount / PAGE_SIZE), 1);
    if (data.items.length === 0 && page.value > lastPage) {
      page.value = lastPage;
      return;
    }
    result.value = data;
    console.log("訂單資料", result.value);
  } catch (err) {
    if (requestId !== latestRequestId) return;
    // 登入失效：登出並回登入頁，登入後回到這一頁（跟路由守衛的處理一致）
    if (err.errorCode === ErrorCodes.NotLoggedIn) {
      redirectToLogin();
      return;
    }
    if (silent) return;
    errorMessage.value = err.message || "讀取訂單資料失敗，請稍後再試";
    result.value = { items: [], totalCount: 0 };
  } finally {
    // 不分一般或安靜模式都要關：安靜模式可能剛好蓋掉使用者按的那一次，不關的話會一直顯示讀取中
    if (requestId === latestRequestId) loading.value = false;
  }
}

// 登入失效：登出並回登入頁，登入後回到這一頁（讀取列表、付款都用這個）
function redirectToLogin() {
  memberStore.logout();
  router.push({ name: "login", query: { redirect: "/member/orders" } });
}

// 條件改變 → 回到第 1 頁再查；已經在第 1 頁時 page 不會變，所以直接查
watch(filters, () => {
  if (page.value !== 1) page.value = 1;
  else load();
});
// 用箭頭函式包起來：watch 會把新舊頁碼當參數傳進去，直接寫 watch(page, load) 會傳到 options
watch(page, () => load());
// 第一次讀完列表，才知道要顯示哪一筆的付款結果
onMounted(async () => {
  await load();
  showPaymentResultIfReturned();
});

// 付款期限到了自動重新讀取，不用重新整理就會看到「已逾期」（說明見 useDueRefresh.js）
useDueRefresh(
  computed(() => result.value.items),
  () => load({ silent: true }),
);

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

// 正在付款的訂單編號：這筆的按鈕轉圈，其他付款按鈕暫時不能按；null 表示沒有在付款
// 不用全站的自動轉圈（loadingAuto）：它只轉到 onPay 結束，但送出表單後瀏覽器還要一下子才換頁，
// 這段時間按鈕會恢復成可以按，客人可能再按一次。所以成功後不清掉，讓它一直轉到離開這一頁
const payingOrderId = ref(null);
const isPaying = (order) => payingOrderId.value === order.orderId;

// 表格、手機卡片、明細視窗三個入口都走這裡：向後端開付款單 → 整頁跳到綠界刷卡頁
async function onPay(order) {
  if (payingOrderId.value !== null) return;
  payingOrderId.value = order.orderId;
  try {
    const checkout = await startPayment(order.orderId);
    submitToEcpay(checkout);
  } catch (err) {
    payingOrderId.value = null;
    handlePayError(err);
  }
}

// 訊息一律用後端的 message：規則只寫在後端，之後改文字也只改一個地方
function handlePayError(err) {
  switch (err.errorCode) {
    case ErrorCodes.InProgress:
      // 上一筆付款還在確認中：重新讀取後會顯示「付款處理中」，付款按鈕也會消失
      toast.add({ title: err.message, color: "warning", icon: "i-mdi-alert-outline" });
      load({ silent: true });
      return;
    case ErrorCodes.AlreadyPaid:
    case ErrorCodes.NotPayable:
    case ErrorCodes.PastStartDeadline:
    case ErrorCodes.NotOnlinePayment:
    case ErrorCodes.Conflict:
    case ErrorCodes.NotFound:
      // 畫面上的狀態已經過時（例如另一個分頁付過了、剛好逾期）：提示後重新讀取
      toast.add({ title: err.message, color: "error", icon: "i-mdi-alert-circle-outline" });
      load({ silent: true });
      return;
    case ErrorCodes.NotLoggedIn:
      redirectToLogin();
      return;
    default:
      // 線上付款暫時不能用（NotConfigured、TradeNoUnavailable）、連不到伺服器、付款網址異常：
      // 訂單本身沒有變，不用重新讀取
      toast.add({
        title: err.message || "無法開始付款，請稍後再試",
        color: "error",
        icon: "i-mdi-alert-circle-outline",
      });
  }
}

// 從綠界按「上一頁」回來：瀏覽器可能直接拿出離開前的畫面（bfcache），付款按鈕會停在轉圈
// event.persisted 為 true 就是這種情況：停止轉圈，並重新讀取（這時付款已經是「付款處理中」）
function onPageShow(event) {
  if (!event.persisted) return;
  payingOrderId.value = null;
  load({ silent: true });
}
onMounted(() => window.addEventListener("pageshow", onPageShow));
onBeforeUnmount(() => window.removeEventListener("pageshow", onPageShow));

// ── 從綠界付完錢回來：跳出付款結果視窗 ──
const resultModal = useOverlay().create(PaymentResultModal);

/**
 * 網址有 ?from=ecpay（後端把客人從綠界帶回來）時，找出 keyword 那一筆，打開付款結果視窗
 * 找不到時（沒有 keyword、列表沒有這一筆）：後端判斷那次的付款結果不能相信，就不會帶訂單編號，
 * 只能請客人稍後查看
 */
async function showPaymentResultIfReturned() {
  if (route.query[RETURN_FROM_ECPAY.key] !== RETURN_FROM_ECPAY.value) return;

  // 一進來就拿掉 from=ecpay，keyword 留著（列表還是只顯示這一筆）：
  // 之後重新整理、從綠界按上一頁回來，都不會再跳一次
  router.replace({ query: { ...route.query, [RETURN_FROM_ECPAY.key]: undefined } });

  const keyword = filters.value.keyword.trim();
  const order = result.value.items.find((item) => item.orderNo === keyword);
  if (!order) {
    toast.add({
      title: "無法確認這次的付款結果",
      description: "請稍後重新整理頁面，查看訂單的付款狀態。",
      color: "warning",
      icon: "i-mdi-alert-outline",
    });
    return;
  }

  const choice = await resultModal.open({ order });

  // 視窗裡看到的是最新的狀態，列表也重新讀一次，兩邊才一致
  load({ silent: true });
  if (choice === "pay") onPay(order);
  else if (choice === "detail") openDetail(order);
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
          <!-- 倒數放在按鈕下方：放在按鈕裡面會讓這一欄太寬
               轉圈、停用由 payingOrderId 控制（原因見 onPay 上方的說明），所以關掉 loadingAuto -->
          <div v-if="isPayable(row.original)" class="flex flex-col items-center gap-0.5">
            <UButton
              color="action"
              size="sm"
              class="whitespace-nowrap"
              :aria-label="`付款：訂單 ${row.original.orderNo}，${remaining(row.original)}`"
              :loading="isPaying(row.original)"
              :disabled="payingOrderId !== null"
              :loading-auto="false"
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
              :loading="isPaying(row)"
              :disabled="payingOrderId !== null"
              :loading-auto="false"
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
