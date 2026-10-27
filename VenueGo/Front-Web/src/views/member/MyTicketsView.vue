<script setup>
import TicketCard from "@/components/tickets/TicketCard.vue";
import {
  getMyTickets,
  createTransfer,
  cancelTransfer,
  redeemTransfer,
  getIncomingTransfers,
  resendTransfer,
} from "@/api/ticketApi";
import TicketQrModal from "@/components/tickets/TicketQrModal.vue";
import { computed, onMounted, ref, watch } from "vue";

const toast = useToast();
const activeTab = ref("all");

const statusCards = [
  { key: "all", label: "全部票券" },
  { key: "available", label: "可使用" },
  { key: "used", label: "已使用" },
  { key: "expired", label: "已過期" },
];

const tabs = [...statusCards, { key: "receive", label: "接收票券" }];

// 撈出所有票券
const tickets = ref([]);
const loading = ref(false);
const errorMsg = ref("");

const receiveTarget = ref(null); // 正在領取哪一筆（只用來顯示票券資訊）

function openReceive(item) {
  receiveTarget.value = item;
  redeemCode.value = "";
  redeemError.value = "";
  redeemSuccess.value = false;
}

function closeReceive() {
  if (redeeming.value) return;
  receiveTarget.value = null;
}

async function loadTickets({ silent = false } = {}) {
  if (!silent) {
    loading.value = true;
    errorMsg.value = "";
  }
  try {
    tickets.value = await getMyTickets();
  } catch (err) {
    console.error("loadTickets 發生錯誤", err);
    errorMsg.value = err.message;
  } finally {
    loading.value = false;
  }
}

const busyId = ref(null);

// 轉贈彈窗（輸入對方 Email）
const transferTarget = ref(null); // 要轉哪一張票
const transferEmail = ref("");
const transferEmailError = ref("");

function openTransfer(ticket) {
  transferTarget.value = ticket;
  transferEmail.value = "";
  transferEmailError.value = "";
}

function closeTransfer() {
  if (busyId.value) return;
  transferTarget.value = null;
}

function submitTransfer() {
  const value = transferEmail.value.trim();
  if (!value) {
    transferEmailError.value = "請輸入對方的 Email";
    return;
  }
  if (!/^\S+@\S+\.\S+$/.test(value)) {
    transferEmailError.value = "Email 格式不正確";
    return;
  }
  startTransfer(transferTarget.value, value);
}

async function startTransfer(ticket, email) {
  if (busyId.value) return;
  busyId.value = ticket.id;
  transferEmailError.value = "";
  try {
    await createTransfer(ticket.id, email);
    toast.add({
      title: "已送出",
      description: "若對方是會員，已寄出通知信，請提醒對方查看信箱。",
      color: "success",
      icon: "i-mdi-check-circle-outline",
    });
    transferTarget.value = null;
    await loadTickets({ silent: true });
  } catch (err) {
    transferEmailError.value = err.message; // 錯誤顯示在彈窗內
  } finally {
    busyId.value = null;
  }
}

async function handleResend(ticket) {
  if (busyId.value) return;
  busyId.value = ticket.id;
  try {
    await resendTransfer(ticket.id);
    toast.add({
      title: "已重新寄出通知信",
      description: "之前的驗證碼已失效，請對方使用最新一封。",
      color: "success",
      icon: "i-mdi-check-circle-outline",
    });
    await loadTickets({ silent: true });
  } catch (err) {
    toast.add({ title: err.message, color: "warning", icon: "i-mdi-alert-outline" });
  } finally {
    busyId.value = null;
  }
}

async function handleCancelTransfer(ticket) {
  if (busyId.value) return;
  busyId.value = ticket.id;
  try {
    await cancelTransfer(ticket.id);
    ticket.transferToMasked = null;
    ticket.transferExpiresAt = null;
    toast.add({ title: "已取消轉贈", color: "success", icon: "i-mdi-check-circle-outline" });
  } catch (err) {
    toast.add({ title: err.message, color: "warning", icon: "i-mdi-alert-outline" });
  } finally {
    busyId.value = null;
  }
}

// 接收
const redeemCode = ref("");
const redeeming = ref(false);
const redeemError = ref("");
const redeemSuccess = ref(false);
const incoming = ref([]);
const incomingError = ref("");

async function loadIncoming() {
  incomingError.value = "";
  try {
    incoming.value = await getIncomingTransfers();
  } catch (err) {
    incomingError.value = err.message;
  }
}

function formatTime(value) {
  return new Date(value).toLocaleString("zh-TW", { hour12: false });
}

watch(activeTab, (key) => {
  if (key === "receive") loadIncoming();
});

async function handleRedeem() {
  const code = redeemCode.value.trim().toUpperCase();
  if (!code) {
    redeemError.value = "請輸入驗證碼";
    return;
  }
  if (redeeming.value) return;

  redeeming.value = true;
  redeemError.value = "";
  redeemSuccess.value = false;
  try {
    await redeemTransfer(code);
    redeemCode.value = "";
    receiveTarget.value = null;
    redeemSuccess.value = true;
    loadTickets({ silent: true });
    loadIncoming();
  } catch (err) {
    redeemError.value = err.message;
  } finally {
    redeeming.value = false;
  }
}

onMounted(loadTickets);

// 分頁篩選規則：「可使用」包含場內，人在場內的票還沒結束，仍屬於可使用
function matchesTab(ticket, key) {
  if (key === "all") return true;
  if (key === "available") return ticket.status === "available" || ticket.status === "inside";
  return ticket.status === key;
}

// 統計卡與 tab 共用 statusCards，數字在這裡算
function countOf(key) {
  return tickets.value.filter((t) => matchesTab(t, key)).length;
}

const filteredTickets = computed(() => tickets.value.filter((t) => matchesTab(t, activeTab.value)));

const qrOpen = ref(false);
const selectedTicket = ref(null);

function openQr(ticket) {
  selectedTicket.value = ticket;
  qrOpen.value = true;
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
        v-for="tab in tabs"
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
    <template v-if="activeTab !== 'receive'">
      <p v-if="loading" class="text-sm text-gray-500 mb-10">載入中…</p>
      <p v-else-if="errorMsg" class="text-sm text-red-600 mb-10">{{ errorMsg }}</p>
      <div v-else-if="filteredTickets.length" class="grid grid-cols-2 gap-4 mb-10">
        <TicketCard
          v-for="t in filteredTickets"
          :key="t.id"
          :ticket="t"
          :busy="busyId === t.id"
          @view-qr="openQr(t)"
          @transfer="openTransfer(t)"
          @cancel-transfer="handleCancelTransfer(t)"
          @transfer-expired="loadTickets({ silent: true })"
          @resend="handleResend(t)"
        />
      </div>
      <p v-else class="text-sm text-gray-500 mb-10">目前沒有符合的票券</p>
    </template>

    <!-- 接收票券 -->
    <section v-else class="border border-gray-200 rounded bg-white p-6">
      <h2 class="text-base font-bold text-gray-900 mb-1">接收票券</h2>
      <p class="text-xs text-gray-500 mb-4">
        驗證碼已寄到你的信箱，按「接收」後輸入驗證碼即可領取。
      </p>

      <p v-if="redeemSuccess" class="mb-4 text-sm text-gray-900">
        領取成功!
        <button
          type="button"
          class="underline"
          @click="
            activeTab = 'all';
            redeemSuccess = false;
          "
        >
          查看我的票券
        </button>
      </p>

      <p v-if="incomingError" class="text-sm text-red-600">{{ incomingError }}</p>
      <div v-else-if="incoming.length" class="flex flex-col gap-3">
        <div
          v-for="(item, i) in incoming"
          :key="i"
          class="rounded border border-gray-400 bg-white p-4 flex justify-between items-center"
        >
          <div>
            <p class="text-sm font-semibold text-gray-900">待接收:{{ item.venueName }}</p>
            <p class="text-xs text-gray-500 my-1">
              轉贈人：{{ item.fromName }} 日期：{{ item.date }} 時段：{{ item.timeRange }}
            </p>
            <span class="inline-block text-xs px-2 py-0.5 border border-gray-300 rounded">
              待接收
            </span>
            <span class="ml-2 text-xs text-gray-500">
              {{ formatTime(item.expiresAt) }} 前領取
            </span>
          </div>
          <button
            class="px-4 py-1.5 text-sm rounded bg-gray-900 text-white hover:bg-gray-700"
            @click="openReceive(item)"
          >
            接收
          </button>
        </div>
      </div>
      <p v-else class="text-sm text-gray-500">目前沒有待領取的轉讓</p>
    </section>

    <!-- 輸入驗證碼視窗 -->
    <div
      v-if="receiveTarget"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      @click.self="closeReceive"
    >
      <div class="w-full max-w-sm rounded-lg bg-white p-6">
        <h2 class="text-base font-bold text-gray-900 mb-1">輸入驗證碼領取</h2>
        <p class="text-sm text-gray-700">{{ receiveTarget.venueName }}</p>
        <p class="text-xs text-gray-500 mb-4">
          {{ receiveTarget.date }} · {{ receiveTarget.timeRange }} · 轉贈人
          {{ receiveTarget.fromName }}
        </p>
        <p class="text-xs text-gray-500 mb-2">請輸入信件中的驗證碼。</p>

        <form class="flex flex-col gap-3" @submit.prevent="handleRedeem">
          <input
            v-model="redeemCode"
            type="text"
            placeholder="例如 AB3K7M9P"
            autocomplete="off"
            class="rounded border border-gray-300 px-3 py-2 font-mono tracking-widest uppercase focus:border-gray-900 focus:outline-none"
            @input="redeemCode = redeemCode.toUpperCase()"
          />
          <p v-if="redeemError" class="text-sm text-red-600">{{ redeemError }}</p>
          <div class="grid grid-cols-2 gap-2">
            <button
              type="button"
              class="py-2 text-sm rounded border border-gray-300 text-gray-600 hover:bg-gray-100"
              @click="closeReceive"
            >
              取消
            </button>
            <button
              type="submit"
              :disabled="redeeming"
              class="py-2 text-sm font-medium rounded bg-gray-900 text-white hover:bg-gray-700 disabled:bg-gray-300"
            >
              {{ redeeming ? "領取中…" : "領取" }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <div
      v-if="transferTarget"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      @click.self="closeTransfer"
    >
      <div class="w-full max-w-sm rounded-lg bg-white p-6">
        <h2 class="text-base font-bold text-gray-900 mb-1">轉贈票券</h2>
        <p class="text-sm text-gray-700">{{ transferTarget.venueName }}</p>
        <p class="text-xs text-gray-500 mb-4">
          {{ transferTarget.date }} · {{ transferTarget.timeRange }}
        </p>
        <p class="text-xs text-gray-500 mb-2">輸入對方的會員 Email，驗證碼會寄到對方信箱。</p>

        <form class="flex flex-col gap-3" @submit.prevent="submitTransfer">
          <input
            v-model="transferEmail"
            type="email"
            placeholder="對方的會員 Email"
            autocomplete="off"
            class="rounded border border-gray-300 px-3 py-2 text-sm focus:border-gray-900 focus:outline-none"
          />
          <p v-if="transferEmailError" class="text-sm text-red-600">{{ transferEmailError }}</p>
          <div class="grid grid-cols-2 gap-2">
            <button
              type="button"
              class="py-2 text-sm rounded border border-gray-300 text-gray-600 hover:bg-gray-100"
              @click="closeTransfer"
            >
              取消
            </button>
            <button
              type="submit"
              :disabled="busyId !== null"
              class="py-2 text-sm font-medium rounded bg-gray-900 text-white hover:bg-gray-700 disabled:bg-gray-300"
            >
              {{ busyId !== null ? "寄送中…" : "寄出" }}
            </button>
          </div>
        </form>
      </div>
    </div>
    <!-- QRcode -->
    <TicketQrModal
      v-model:open="qrOpen"
      :ticket="selectedTicket"
      @status-changed="loadTickets({ silent: true })"
    />
  </div>
</template>
<style scoped></style>
