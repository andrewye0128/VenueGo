<script setup>
import { computed, onUnmounted, ref, watch } from "vue";
// 10/25 評論組（昱）新增：已使用的票券，「轉贈票券」的位置改放現場評論的按鈕（立刻評論／查看評論）
import { useReviewStatuses } from "@/composables/useReviewStatuses";

const props = defineProps({
  ticket: { type: Object, required: true },
  busy: { type: Boolean, default: false },
});

const emit = defineEmits(["view-qr", "transfer", "cancel-transfer", "transfer-expired"]);

// 10/25 評論組（昱）新增：已使用、而且這張票可以評論（或已經評過）時才有值；其他情況是 null，維持原本的「轉贈票券」。
// 一筆訂單有好幾張票時，只有判斷資格用的那一張有按鈕（每筆訂單只能評一次），說明在 composables/useReviewStatuses.js
const reviewStatus = useReviewStatuses();
const reviewButton = computed(() =>
  props.ticket.status === "used" ? reviewStatus.visitFor(props.ticket.id) : null,
);

const statusMap = {
  available: { label: "可使用", class: "bg-blue-800 text-white" },
  used: { label: "已使用", class: "bg-gray-100 text-gray-500" },
  expired: { label: "已過期", class: "border border-gray-200 bg-gray-100 text-gray-500" },
  transferred: { label: "已轉贈", class: "bg-gray-100 text-gray-500" },
};

const placeholderText = computed(() => {
  switch (props.ticket.status) {
    case "available":
      return "想送給朋友?點下方轉贈,產生驗證碼";
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

const isTransferring = computed(() => !!props.ticket.transferCode); // !!
const canUse = computed(() => props.ticket.status === "available" && !isTransferring.value);

// 轉贈中優先顯示 -> 再顯示其他狀態
const badge = computed(() =>
  isTransferring.value
    ? { label: "轉贈中", class: "bg-gray-900 text-white" }
    : statusMap[props.ticket.status],
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
const copied = ref(false);
async function copyCode() {
  try {
    await navigator.clipboard.writeText(props.ticket.transferCode);
    copied.value = true;
    setTimeout(() => (copied.value = false), 1500);
  } catch (err) {
    console.error("複製失敗", err);
  }
}

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
    <!-- 轉贈中:驗證碼區塊 -->
    <div
      class="mt-2 flex h-14 items-center rounded border border-dashed px-3"
      :class="
        isTransferring
          ? 'border-gray-400 bg-gray-50 justify-between'
          : 'border-gray-200 justify-center'
      "
    >
      <template v-if="isTransferring">
        <span class="font-mono text-lg font-bold tracking-[0.25em] text-gray-900 select-all">
          {{ ticket.transferCode }}
        </span>
        <span class="text-xs" :class="noTime ? 'text-red-600 font-medium' : 'text-gray-500'">
          剩 {{ remainText }}
        </span>
      </template>
      <span v-else class="text-xs text-gray-400">{{ placeholderText }}</span>
    </div>
    <div class="grid grid-cols-2 gap-2 mt-2">
      <template v-if="isTransferring">
        <button
          type="button"
          class="py-2 text-sm font-medium rounded border bg-gray-900 text-white border-gray-900 hover:bg-gray-700 transition-colors"
          @click="copyCode"
        >
          {{ copied ? "已複製 ✓" : "複製驗證碼" }}
        </button>

        <button
          v-if="!confirmCancel"
          :disabled="busy"
          type="button"
          class="py-2 text-sm font-medium rounded border bg-white text-gray-900 border-gray-400 hover:bg-gray-100 transition-colors"
          @click="confirmCancel = true"
        >
          取消轉贈
        </button>
        <div v-else class="grid grid-cols-2 gap-1">
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
      </template>
      <template v-else>
        <button
          type="button"
          :disabled="!canUse"
          class="py-2 text-sm font-medium rounded border transition-colors"
          :class="
            canUse
              ? 'bg-gray-900 text-white border-gray-900 hover:bg-gray-700'
              : 'bg-white text-gray-300 border-gray-200 cursor-not-allowed'
          "
          @click="emit('view-qr')"
        >
          查看 QR Code
        </button>
        <!-- 10/25 評論組（昱）新增：已使用的票券，轉贈的位置改放評論按鈕（已使用的票本來就不能轉贈，兩顆不會同時需要）。
             顏色跟我的預約、我的評論一樣：立刻評論＝橘色實心、查看評論＝藍色外框（見 useReviewStatuses.js）。
             不用這張卡片的黑色：那是報到組給「查看 QR Code」用的，兩顆一樣黑會分不出來。
             h-auto py-2：高度跟旁邊的「查看 QR Code」一樣（UButton 預設高 40px，這張卡片的按鈕是 38px） -->
        <UButton
          v-if="reviewButton"
          :to="reviewButton.to"
          :color="reviewButton.color"
          :variant="reviewButton.variant"
          :label="reviewButton.label"
          block
          class="h-auto py-2"
        />
        <template v-else>
          <button
            type="button"
            :disabled="!canUse || busy"
            class="py-2 text-sm font-medium rounded border transition-colors"
            :class="
              canUse && !busy
                ? 'bg-white text-gray-900 border-gray-400 hover:bg-gray-100'
                : 'bg-white text-gray-300 border-gray-200 cursor-not-allowed'
            "
            @click="emit('transfer')"
          >
            {{ busy ? "處理中…" : "轉贈票券" }}
          </button>
        </template>
      </template>
    </div>
  </div>
</template>
<style scoped></style>
