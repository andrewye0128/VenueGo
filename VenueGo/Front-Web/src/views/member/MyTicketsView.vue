<script setup>
import TicketCard from "@/components/tickets/TicketCard.vue";
import { getMyTickets } from "@/api/ticketApi";
import TicketQrModal from "@/components/tickets/TicketQrModal.vue";
import { computed, onMounted, ref } from "vue";

const activeTab = ref("all");

const statusCards = [
  { key: "all", label: "全部票券" },
  { key: "available", label: "可使用" },
  { key: "used", label: "已使用" },
  { key: "expired", label: "已過期" },
];

// 撈出所有票券
const tickets = ref([]);
const loading = ref(false);
const errorMsg = ref("");

async function loadTickets() {
  loading.value = true;
  errorMsg.value = "";
  try {
    tickets.value = await getMyTickets(3);
  } catch (err) {
    console.error("loadTickets 發生錯誤", err);
    errorMsg.value = err.message;
  } finally {
    loading.value = false;
  }
}

onMounted(loadTickets);

// 統計卡與 tab 共用 statusCards，數字在這裡算
function countOf(key) {
  return key === "all"
    ? tickets.value.length
    : tickets.value.filter((t) => t.status === key).length;
}

const filteredTickets = computed(() =>
  activeTab.value === "all"
    ? tickets.value
    : tickets.value.filter((t) => t.status === activeTab.value),
);

// const tickets = ref([
//   {
//     id: 1,
//     venueName: "羽球場 A",
//     sportType: "羽球",
//     date: "2026/09/30",
//     timeRange: "18:00-20:00",
//     status: "available",
//   },
//   {
//     id: 2,
//     venueName: "第一籃球場",
//     sportType: "籃球",
//     date: "2026/09/10",
//     timeRange: "20:00-21:00",
//     status: "used",
//   },
//   {
//     id: 3,
//     venueName: "桌球室 3 號台",
//     sportType: "桌球",
//     date: "2026/09/18",
//     timeRange: "09:00-10:00",
//     status: "transferred",
//   },
// ]);

// 依票券分類
// const stats = computed(() => [
//   { label: "全部票券", value: tickets.value.length },
//   {
//     label: "可使用",
//     value: tickets.value.filter((t) => t.status === "available").length,
//   },
//   {
//     label: "已使用",
//     value: tickets.value.filter((t) => t.status === "used").length,
//   },
//   {
//     label: "已轉贈",
//     value: tickets.value.filter((t) => t.status === "transferred").length,
//   },
// ]);

const receivedtickets = ref([
  {
    id: 1,
    venueName: "綜合排球場",
    from: "陳美華",
    date: "2026/10/05",
    timeRange: "16:00-17:00",
    status: "pending",
  },
  {
    id: 2,
    venueName: "羽球場 B",
    from: "林建宏",
    date: "2026/09/22",
    timeRange: "10:00-11:00",
    status: "accepted",
  },
  {
    id: 3,
    venueName: "桌球室 2 號台",
    from: "張裕婷",
    date: "2026/09/15",
    timeRange: "14:00-15:00",
    status: "rejected",
  },
]);

const transferStatusMap = {
  pending: { label: "待接收", prefix: "待接收" },
  accepted: { label: "已接收", prefix: "已接收" },
  rejected: { label: "已拒絕", prefix: "已拒絕" },
};

const qrOpen = ref(false);
const selectedTicket = ref(null);

function openQr(ticket) {
  selectedTicket.value = ticket;
  qrOpen.value = true;
}

function startTransfer(ticket) {
  console.log("轉贈票券", ticket.id);
}
</script>
<template>
  <div>
    <h1 class="text-2xl font-bold text-gray-900 mb-6">我的票券</h1>

    <!-- 統計卡 -->
    <div class="grid grid-cols-4 gap-4 mb-6">
      <div
        v-for="s in statusCards"
        :key="s.key"
        class="border border-gray-200 rounded bg-white py-6 flex flex-col items-center"
      >
        <span class="text-2xl font-bold text-gray-900">{{ countOf(s.key) }}</span>
        <span class="text-xs text-gray-500 mt-1">{{ s.label }}</span>
      </div>
    </div>

    <div class="flex gap-1 mb-6 border-b border-gray-200">
      <button
        v-for="tab in statusCards"
        :key="tab.key"
        class="px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px"
        :class="
          activeTab === tab.key
            ? 'border-gray-900 text-gray-900'
            : 'border-transparent text-gray-500 hover:text-gray-700'
        "
        @click="activeTab = tab.key"
      >
        {{ tab.label }}
      </button>
    </div>

    <!-- 票券列表 -->
    <p v-if="loading" class="text-sm text-gray-500 mb-10">載入中…</p>
    <p v-else-if="errorMsg" class="text-sm text-red-600 mb-10">{{ errorMsg }}</p>
    <div v-else-if="filteredTickets.length" class="grid grid-cols-2 gap-4 mb-10">
      <TicketCard
        v-for="t in filteredTickets"
        :key="t.id"
        :ticket="t"
        @view-qr="openQr(t)"
        @transfer="startTransfer(t)"
      />
    </div>
    <p v-else class="text-sm text-gray-500 mb-10">目前沒有符合的票券</p>

    <!-- 接收票券 -->
    <section class="border border-gray-200 rounded bg-white p-6">
      <h2 class="text-base font-bold text-gray-900 mb-4">接收票券</h2>

      <div class="flex flex-col gap-3">
        <div
          v-for="item in receivedtickets"
          :key="item.id"
          class="rounded border p-4 flex justify-between items-center"
          :class="
            item.status === 'pending'
              ? 'border-gray-400 bg-white'
              : 'border-gray-100 bg-gray-50 text-gray-400'
          "
        >
          <div>
            <p
              class="text-sm font-semibold"
              :class="item.status === 'pending' ? 'text-gray-900' : 'text-gray-500'"
            >
              {{ transferStatusMap[item.status].prefix }}:{{ item.venueName }}
            </p>
            <p class="text-xs text-gray-500 my-1">
              轉贈人：{{ item.from }} 日期：{{ item.date }} 時段：{{ item.timeRange }}
            </p>
            <span class="inline-block text-xs px-2 py-0.5 border border-gray-300 rounded">
              {{ transferStatusMap[item.status].label }}
            </span>
          </div>
          <!-- 只有待接收才有按鈕 -->
          <div v-if="item.status === 'pending'" class="flex gap-2">
            <button
              class="px-4 py-1.5 text-sm border border-gray-400 rounded text-gray-700 hover:bg-gray-100"
              @click="rejectTransfer(item)"
            >
              拒絕
            </button>
            <button
              class="px-4 py-1.5 text-sm rounded bg-gray-900 text-white hover:bg-gray-700"
              @click="acceptTransfer(item)"
            >
              接收
            </button>
          </div>
        </div>
      </div>
    </section>

    <!-- QRcode -->
    <TicketQrModal v-model:open="qrOpen" :ticket="selectedTicket" />
  </div>
</template>
<style scoped></style>
