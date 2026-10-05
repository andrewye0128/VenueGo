<script setup>
import { computed } from "vue";

const props = defineProps({
  ticket: { type: Object, default: null },
});

// v-model:open：父層用 v-model:open 綁定開關
const open = defineModel("open", { type: Boolean, default: false });

const statusMap = {
  available: { label: "可使用", class: "border border-gray-900 text-gray-900" },
  used: { label: "已使用", class: "border border-gray-200 bg-gray-100 text-gray-500" },
  expired: { label: "已過期", class: "border border-gray-200 bg-gray-100 text-gray-500" },
  transferred: { label: "已轉贈", class: "border border-gray-200 bg-gray-100 text-gray-500" },
};

const badge = computed(() => (props.ticket ? statusMap[props.ticket.status] : null));
const isAvailable = computed(() => props.ticket?.status === "available");
</script>

<template>
  <UModal
    v-model:open="open"
    title="入場 QR Code"
    description="出示此 QR Code 供櫃檯掃描入場"
    :ui="{ description: 'sr-only', content: 'max-w-sm' }"
  >
    <template #body>
      <div v-if="ticket" class="flex flex-col gap-4">
        <!-- QR 區塊：維持白底，留白才掃得到 -->
        <div
          class="relative mx-auto flex aspect-square w-full max-w-60 items-center justify-center rounded border border-gray-200 bg-white p-4"
        >
          <!-- 先用 icon 當佔位，之後換成真的 QR 圖 -->
          <UIcon name="i-lucide-qr-code" class="h-full w-full text-gray-400" />

          <!-- 非可使用狀態：蓋掉 QR，避免拿舊 QR 去掃 -->
          <div
            v-if="!isAvailable"
            class="absolute inset-0 flex items-center justify-center rounded bg-white/90 text-sm font-medium text-gray-500"
          >
            此票券已失效
          </div>
        </div>

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
          入場說明：請於報到櫃檯出示本 QR Code 供掃描，開放入場時間為使用時段前 15 分鐘。
        </p>
      </div>
    </template>
  </UModal>
</template>
