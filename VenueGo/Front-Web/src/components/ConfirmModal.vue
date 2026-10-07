<!--
  ConfirmModal.vue — 全站通用的確認視窗（昱）：按鈕的數量、文字、顏色都由呼叫端決定
  （評論頁用在送出確認、覆蓋草稿、離開前三選一）

  用法（useOverlay 是 Nuxt UI 的函式，不用 import）：
    const confirmModal = useOverlay().create(ConfirmModal);
    const choice = await confirmModal.open({
      title: "尚未儲存",
      description: "你填寫的內容還沒有儲存，離開這一頁之後會消失。",
      actions: [
        { value: "cancel", label: "留在這頁", variant: "outline" },
        { value: "save", label: "儲存草稿並離開" },
      ],
    });
    // choice 是被按下那顆的 value；按 Esc、點外面、按右上角 X → undefined（當成取消）

  ── 為什麼 open() 回傳 Promise ─────────────────────────
  路由守衛要「等使用者選完」才能決定放不放行，寫成 await 讀起來是一條直線。

  ── 手機 ────────────────────────────────────────────────
  窄螢幕（< md）按鈕改成直排、滿版寬、大尺寸：橫排時「不儲存，直接離開」和「儲存草稿並離開」
  會擠在一起，手指容易按錯，而按錯的代價是草稿消失。直排順序跟 HTML 順序一致，
  Tab 鍵的焦點順序和眼睛看到的上下順序相同。
-->
<script setup>
defineProps({
  title: { type: String, required: true },
  description: { type: String, default: "" },
  // 每顆按鈕：{ value, label, color?, variant? }。第一顆是預設焦點，請放最安全的選項。
  actions: { type: Array, required: true },
});

const emit = defineEmits(["close"]);
</script>

<template>
  <UModal :title="title" :description="description" :close="false">
    <template #footer>
      <div class="flex w-full flex-col gap-2 md:flex-row md:flex-wrap md:justify-end">
        <UButton
          v-for="(action, i) in actions"
          :key="action.value"
          :label="action.label"
          :color="action.color ?? 'primary'"
          :variant="action.variant ?? 'solid'"
          :autofocus="i === 0"
          class="w-full md:w-auto"
          @click="emit('close', action.value)"
        />
      </div>
    </template>
  </UModal>
</template>
