<script setup>
import { computed, onUnmounted, ref, watch } from "vue";
// import { errorMessages } from "vue/compiler-sfc";

const props = defineProps({
  ticket: { type: Object, required: true },
  busy: { type: Boolean, default: false },
});

const emit = defineEmits(["view-qr", "transfer", "resend", "cancel-transfer", "transfer-expired"]);

const statusMap = {
  available: { label: "可使用", class: "bg-blue-800 text-white" },
  inside: { label: "場內", class: "bg-green-700 text-white" },
  used: { label: "已使用", class: "bg-gray-100 text-gray-500" },
  expired: { label: "已過期", class: "border border-gray-200 bg-gray-100 text-gray-500" },
  transferred: { label: "已轉贈", class: "bg-gray-100 text-gray-500" },
};

const unknownBadge = { label: "狀態未知", class: "bg-gray-100 text-gray-500" };

const placeholderText = computed(() => {
  switch (props.ticket.status) {
    case "available":
      return "想送給朋友?點下方轉贈,輸入對方 Email";
    case "inside":
      return "您目前在場內，離場時請出示 QR Code";
    case "used":
      return "此票券已使用";
    case "expired":
      return "此票券已過期";
    case "transferred":
      return "此票券已轉贈給他人";
    default:
      return "";
  }
});

const isTransferring = computed(() => !!props.ticket.transferToMasked);

// 能出示 QR：可使用（入場用）或場內（離場用）；轉贈中也可以（後端規則：轉贈完成前持有人 QR 仍有效）
const canShowQr = computed(
  () => props.ticket.status === "available" || props.ticket.status === "inside",
);
// 能轉贈：只有可使用的票，場內的人不能轉贈
const canTransfer = computed(() => props.ticket.status === "available" && !isTransferring.value);

// 轉贈中優先顯示 -> 再顯示其他狀態
const badge = computed(() =>
  isTransferring.value
    ? { label: "轉贈中", class: "bg-gray-900 text-white" }
    : (statusMap[props.ticket.status] ?? unknownBadge),
);

// 倒數計時
const now = ref(Date.now());
let timer = null;

watch(
  isTransferring,
  (val) => {
    clearInterval(timer);
    if (val) {
      timer = setInterval(() => (now.value = Date.now()), 1000);
    }
  },
  { immediate: true },
);
onUnmounted(() => clearInterval(timer));

const remainMs = computed(() => new Date(props.ticket.transferExpiresAt).getTime() - now.value);
const noTime = computed(() => remainMs.value < 5 * 60 * 1000);

const remainText = computed(() => {
  const sec = Math.max(0, Math.floor(remainMs.value / 1000));
  const h = Math.floor(sec / 3600);
  const m = Math.floor((sec % 3600) / 60);
  const s = sec % 60;
  const mm = String(m).padStart(2, "0");
  const ss = String(s).padStart(2, "0");
  return h > 0 ? `${h} 小時 ${mm} 分` : `${mm}:${ss}`;
});

watch(
  () => isTransferring.value && remainMs.value <= 0,
  (expired) => {
    if (expired) {
      emit("transfer-expired");
    }
  },
  { immediate: true },
);

// ---- 複製驗證碼 ----
// const copied = ref(false);
// async function copyCode() {
//   try {
//     await navigator.clipboard.writeText(props.ticket.transferCode);
//     copied.value = true;
//     setTimeout(() => (copied.value = false), 1500);
//   } catch (err) {
//     console.error("複製失敗", err);
//   }
// }

// // 轉贈表單(輸入對方會員email)
// const showForm = ref(false);
// const email = ref("");
// const emailError = ref("");

// function submitTransfer() {
//   const value = email.value.trim();
//   if (!value) {
//     emailError.value = "請輸入對方的 Email";
//     return;
//   }
//   if (!/^\S+@\S+\.\S+$/.test(value)) {
//     emailError.value = "Email 格式不正確";
//     return;
//   }
//   emailError.value = "";
//   // 成功由父層呼叫done(),才關閉
//   emit("transfer", value, () => {
//     showForm.value = false;
//     email.value = "";
//   });
// }

// 取消轉贈
const confirmCancel = ref(false);
function onCancel() {
  confirmCancel.value = false;
  emit("cancel-transfer");
}
</script>
<template>
  <div class="border border-gray-200 rounded-lg bg-white p-4 flex flex-col gap-2">
    <div class="flex justify-between items-start">
      <h3 class="text-base font-semibold text-gray-900">
        {{ ticket.venueName }}
      </h3>
      <span class="text-xs px-2 py-0.5 rounded-full font-medium" :class="badge.class">
        {{ badge.label }}
      </span>
    </div>

    <p class="text-sm text-gray-500">{{ ticket.sportType }}</p>
    <p class="text-sm text-gray-500">{{ ticket.date }} · {{ ticket.timeRange }}</p>
    <!-- 轉贈中:顯示寄給誰;平常顯示提示 -->
    <div
      class="mt-2 flex min-h-14 items-center rounded border border-dashed px-3 py-2"
      :class="
        isTransferring
          ? 'border-gray-400 bg-gray-50 justify-between'
          : 'border-gray-200 justify-center'
      "
    >
      <template v-if="isTransferring">
        <span class="text-sm text-gray-900">已寄給 {{ ticket.transferToMasked }}</span>
        <span class="text-xs" :class="noTime ? 'text-red-600 font-medium' : 'text-gray-500'">
          剩 {{ remainText }}
        </span>
      </template>
      <span v-else class="text-xs text-gray-400">{{ placeholderText }}</span>
    </div>
    <div class="grid grid-cols-2 gap-2 mt-2">
      <template v-if="isTransferring">
        <template v-if="!confirmCancel">
          <button
            type="button"
            :disabled="busy"
            class="py-2 text-sm font-medium rounded border bg-white text-gray-900 border-gray-400 hover:bg-gray-100 transition-colors"
            @click="emit('resend')"
          >
            {{ busy ? "處理中…" : "重新寄送" }}
          </button>
          <button
            :disabled="busy"
            type="button"
            class="py-2 text-sm font-medium rounded border bg-white text-gray-900 border-gray-400 hover:bg-gray-100 transition-colors"
            @click="confirmCancel = true"
          >
            取消轉贈
          </button>
        </template>
        <div v-else class="col-span-2 grid grid-cols-2 gap-1">
          <button
            type="button"
            class="py-2 text-xs rounded border border-gray-300 text-gray-600 hover:bg-gray-100"
            @click="confirmCancel = false"
          >
            再想想
          </button>
          <button
            type="button"
            :disabled="busy"
            class="py-2 text-xs rounded bg-red-600 text-white hover:bg-red-700"
            @click="onCancel"
          >
            {{ busy ? "取消中…" : "確定取消" }}
          </button>
        </div>
        <button
          type="button"
          class="col-span-2 py-2 text-sm font-medium rounded border bg-white text-gray-900 border-gray-400 hover:bg-gray-100 transition-colors"
          @click="emit('view-qr')"
        >
          使用票券
        </button>
      </template>
      <template v-else>
        <button
          type="button"
          :disabled="!canShowQr"
          class="py-2 text-sm font-medium rounded border transition-colors"
          :class="
            canShowQr
              ? 'bg-gray-900 text-white border-gray-900 hover:bg-gray-700'
              : 'bg-white text-gray-300 border-gray-200 cursor-not-allowed'
          "
          @click="emit('view-qr')"
        >
          使用票券
        </button>
        <button
          type="button"
          :disabled="!canTransfer || busy"
          class="py-2 text-sm font-medium rounded border transition-colors"
          :class="
            canTransfer && !busy
              ? 'bg-white text-gray-900 border-gray-400 hover:bg-gray-100'
              : 'bg-white text-gray-300 border-gray-200 cursor-not-allowed'
          "
          @click="emit('transfer')"
        >
          {{ busy ? "處理中…" : "轉贈票券" }}
        </button>
      </template>
    </div>
  </div>
</template>
<style scoped></style>
