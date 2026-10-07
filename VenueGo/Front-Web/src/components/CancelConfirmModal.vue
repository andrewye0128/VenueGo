<!--
  CancelConfirmModal.vue — 全站通用的「取消確認」視窗（取消預約、之後的取消訂單共用）

  跟 ConfirmModal.vue 的差別：
  - 要取消的資料用左右兩欄排整齊（details）
  - 可以顯示提醒（notice，例如已付款要退款）
  - 有「取消原因（選填）」輸入框
  - 按「確定取消」後視窗不關，按鈕轉圈等 action 跑完；失敗時在視窗裡顯示錯誤，可以再按一次

  用法（useOverlay 是 Nuxt UI 的函式，不用 import）：
    const cancelModal = useOverlay().create(CancelConfirmModal);
    const done = await cancelModal.open({
      title: "取消預約",
      description: "確定要取消以下預約嗎？",
      details: [{ label: "預約編號", value: "R012" }, { label: "場地名稱", value: "羽球場 A" }],
      notice: "此預約已付款 NT$ 800，…",          // 選填，不傳就不顯示
      action: (reason) => cancelMyReservation(12, reason),
    });
    // done：取消成功 → true；按「再想想」、右上角 ✕、Esc、點外面 → undefined

  action 收到的 reason：去掉前後空白後的文字；沒填（或只有空白）是 null
-->
<script setup>
import { ref } from "vue";

const props = defineProps({
  title: { type: String, required: true },
  description: { type: String, default: "" },
  // 要取消的資料：[{ label, value }]
  details: { type: Array, default: () => [] },
  // 提醒文字（選填）
  notice: { type: String, default: "" },
  confirmLabel: { type: String, default: "確定取消" },
  cancelLabel: { type: String, default: "再想想" },
  reasonPlaceholder: { type: String, default: "例如：臨時有事無法前往" },
  // 取消原因上限：資料庫 Reservations.CancelReason 是 nvarchar(200)，後台也是限制 200 字
  maxLength: { type: Number, default: 200 },
  // 按「確定取消」時執行的函式（要回傳 Promise）；丟出錯誤時，錯誤訊息會顯示在視窗裡
  action: { type: Function, required: true },
});

const emit = defineEmits(["close"]);

const reason = ref("");
const submitting = ref(false);
const errorMessage = ref("");

// 打開時把焦點放在「再想想」：
// 視窗預設會聚焦「第一個可以操作的元素」，也就是取消原因輸入框，按鈕上的 autofocus 沒有作用；
// 聚焦在輸入框的話，手機一打開視窗就會跳出鍵盤，擋住半個畫面
const keepButton = ref(null);
const modalContent = {
  onOpenAutoFocus: (event) => {
    event.preventDefault();
    keepButton.value?.$el?.focus();
  },
};

async function confirm() {
  errorMessage.value = "";
  submitting.value = true;
  try {
    await props.action(reason.value.trim() || null);
    emit("close", true);
  } catch (err) {
    // http.js 已經把錯誤整理成中文的 err.message
    errorMessage.value = err.message || "取消失敗，請稍後再試";
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <!-- dismissible：處理中不能用 Esc、點外面關閉，避免使用者以為取消了、其實還在處理
       close：右上角 ✕ 關閉按鈕，效果等於「再想想」
         - disabled：✕ 會直接關閉視窗、不看 dismissible，所以處理中要自己停用
         - aria-label：Nuxt UI 預設的讀屏文字是英文 "Close"，改成中文 -->

  <UModal
    :title="title"
    :description="description"
    :dismissible="!submitting"
    :close="{ disabled: submitting, 'aria-label': '關閉' }"
    :content="modalContent"
  >
    <template #body>
      <div class="flex flex-col gap-4">
        <!-- 要取消的資料 -->
        <dl
          v-if="details.length"
          class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 rounded bg-brand-background p-4 text-sm"
        >
          <template v-for="item in details" :key="item.label">
            <dt class="text-neutral-text-secondary">{{ item.label }}</dt>
            <dd class="font-medium text-neutral-text-primary">{{ item.value }}</dd>
          </template>
        </dl>

        <UAlert
          v-if="notice"
          color="warning"
          variant="soft"
          icon="i-mdi-alert-outline"
          :description="notice"
        />

        <UFormField label="取消原因（選填）">
          <UTextarea
            v-model="reason"
            :maxlength="maxLength"
            :placeholder="reasonPlaceholder"
            :rows="3"
            :disabled="submitting"
            class="w-full"
          />
          <!-- 字數：maxlength 讓輸入框打不進超過的字，這裡只負責顯示 -->
          <p class="mt-1 text-end text-xs text-neutral-text-secondary">
            {{ reason.length }} / {{ maxLength }}
          </p>
        </UFormField>

        <UAlert
          v-if="errorMessage"
          color="error"
          variant="soft"
          icon="i-mdi-alert-circle"
          :description="errorMessage"
        />
      </div>
    </template>

    <!-- 按鈕：手機直排滿版（不容易按錯），平板以上靠右橫排；跟 ConfirmModal 一致
         第一顆「再想想」是預設焦點：不小心按 Enter 也不會誤取消 -->
    <template #footer>
      <div class="flex w-full flex-col gap-2 md:flex-row md:justify-end">
        <UButton
          ref="keepButton"
          :label="cancelLabel"
          color="primary"
          variant="outline"
          :disabled="submitting"
          class="w-full md:w-auto"
          @click="emit('close')"
        />
        <UButton
          :label="confirmLabel"
          color="error"
          :loading="submitting"
          class="w-full md:w-auto"
          @click="confirm"
        />
      </div>
    </template>
  </UModal>
</template>
