<script setup>
import { computed, onUnmounted, ref, watch } from "vue";
import QRCode from "qrcode";
import { getTicketQr } from "@/api/ticketApi";

const props = defineProps({
  ticket: { type: Object, default: null },
});

// 後端回來的狀態跟列表上的不一樣時（例如剛被櫃台掃進場），通知父層重新載入列表
const emit = defineEmits(["status-changed"]);

// v-model:open：父層用 v-model:open 綁定開關
const open = defineModel("open", { type: Boolean, default: false });
const REFRESH_SECONDS = 20;

const statusMap = {
  available: { label: "可使用", class: "border border-gray-900 text-gray-900" },
  inside: { label: "場內", class: "border border-gray-900 text-gray-900" },
  used: { label: "已使用", class: "border border-gray-200 bg-gray-100 text-gray-500" },
  expired: { label: "已過期", class: "border border-gray-200 bg-gray-100 text-gray-500" },
  transferred: { label: "已轉贈", class: "border border-gray-200 bg-gray-100 text-gray-500" },
};

const unknownBadge = {
  label: "狀態未知",
  class: "border border-gray-200 bg-gray-100 text-gray-500",
};

// ---- 狀態 ----
const loading = ref(false);
const errorMsg = ref("");
const qrImage = ref(""); // QR 圖片網址（data URL）
const qrStatus = ref(""); // 最新一次後端回的狀態：available / inside
const secondsLeft = ref(REFRESH_SECONDS);

let timer = null;
let requestId = 0; // 每次請求編號，慢的請求晚回來時不要蓋掉新的結果
let knownStatus = ""; // 目前已知的票券狀態，用來判斷要不要通知父層

// 徽章：優先用後端最新回的狀態，還沒回來就先用列表上的
const badge = computed(() => {
  const status = qrStatus.value || props.ticket?.status;
  return statusMap[status] ?? unknownBadge;
});

const isInside = computed(() => qrStatus.value === "inside");
const title = computed(() => (isInside.value ? "離場 QR Code" : "入場 QR Code"));
const description = computed(() =>
  isInside.value ? "出示此 QR Code 供櫃檯掃描離場" : "出示此 QR Code 供櫃檯掃描入場",
);

// ---- 取得並畫出 QR ----
async function fetchQr() {
  const myRequest = ++requestId;
  try {
    const data = await getTicketQr(props.ticket.id);
    const image = await QRCode.toDataURL(data.payload, {
      errorCorrectionLevel: "M",
      margin: 1,
      width: 480,
    });
    if (myRequest !== requestId) return; // 已經有更新的請求了，這份作廢

    qrImage.value = image;
    qrStatus.value = data.status;
    errorMsg.value = "";
    notifyIfStatusChanged(data.status);
  } catch (err) {
    if (myRequest !== requestId) return;

    qrImage.value = ""; // 失敗時不要留著可能已過期的舊 QR
    if (err.errorCode === "QR_UNAVAILABLE") {
      // 票現在不能出示 QR（例如已經離場完成）：停止更新，並通知列表更新狀態
      errorMsg.value = "此票券目前無法出示 QR Code";
      stopTimer();
      emit("status-changed");
    } else {
      // 網路問題等：保留計時器，下一輪會自動再試
      errorMsg.value = err.message || "QR Code 載入失敗";
    }
  } finally {
    if (myRequest === requestId) loading.value = false;
  }
}

function notifyIfStatusChanged(newStatus) {
  if (newStatus !== knownStatus) {
    knownStatus = newStatus;
    emit("status-changed");
  }
}

// ---- 計時器：每秒倒數，到 0 就重新取得 QR ----
function startTimer() {
  stopTimer();
  secondsLeft.value = REFRESH_SECONDS;
  timer = setInterval(() => {
    secondsLeft.value -= 1;
    if (secondsLeft.value <= 0) {
      secondsLeft.value = REFRESH_SECONDS;
      fetchQr();
    }
  }, 1000);
}

function stopTimer() {
  clearInterval(timer);
  timer = null;
}

// ---- 視窗打開 / 關閉 ----
function startSession() {
  qrImage.value = "";
  qrStatus.value = "";
  errorMsg.value = "";
  loading.value = true;
  knownStatus = props.ticket?.status ?? "";
  startTimer();
  fetchQr();
}

function endSession() {
  stopTimer();
  requestId++; // 讓還沒回來的請求作廢
  loading.value = false;
}

watch(open, (isOpen) => {
  if (isOpen && props.ticket) {
    startSession();
  } else {
    endSession();
  }
});

// 元件被銷毀（例如離開頁面）時一定要清掉計時器，不然會在背景一直打 API
onUnmounted(endSession);
</script>

<template>
  <UModal
    v-model:open="open"
    :title="title"
    :description="description"
    :ui="{ description: 'sr-only', content: 'max-w-sm' }"
  >
    <template #body>
      <div v-if="ticket" class="flex flex-col gap-4">
        <!-- QR 區塊：維持白底，留白才掃得到 -->
        <div
          class="relative mx-auto flex aspect-square w-full max-w-60 items-center justify-center rounded border border-gray-200 bg-white p-4"
        >
          <img v-if="qrImage" :src="qrImage" alt="票券 QR Code" class="h-full w-full" />
          <!-- 先用 icon 當佔位，之後換成真的 QR 圖 -->
          <!-- 載入中 -->
          <UIcon
            v-else-if="loading"
            name="i-lucide-loader-circle"
            class="h-10 w-10 animate-spin text-gray-400"
          />

          <!-- 失敗：不顯示舊 QR，避免拿過期的去掃 -->
          <div
            v-if="errorMsg"
            class="absolute inset-0 flex items-center justify-center rounded bg-white/90 p-4 text-center text-sm font-medium text-gray-500"
          >
            {{ errorMsg }}
          </div>
        </div>

        <!-- 自動更新倒數 -->
        <p v-if="qrImage" class="text-center text-xs text-gray-500">
          QR Code 將在 {{ secondsLeft }} 秒後自動更新
        </p>

        <!-- 票券資訊 -->
        <div>
          <h3 class="text-base font-semibold text-gray-900">{{ ticket.venueName }}</h3>
          <p class="mt-1 text-sm text-gray-500">
            {{ ticket.sportType }} · {{ ticket.date }} · {{ ticket.timeRange }}
          </p>
          <span
            class="mt-2 inline-block rounded px-2 py-0.5 text-xs font-medium"
            :class="badge.class"
          >
            {{ badge.label }}
          </span>
        </div>

        <p class="text-xs leading-relaxed text-gray-500">
          使用說明：請於預約時段開始後，向報到櫃檯出示本 QR Code 供掃描。QR Code
          會定時自動更新，請勿截圖使用。
        </p>
      </div>
    </template>
  </UModal>
</template>
