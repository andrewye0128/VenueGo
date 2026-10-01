<script setup>
import { computed } from "vue";

const props = defineProps({
  ticket: { type: Object, required: true },
});

const emit = defineEmits(["view-qr", "transfer"]);

const statusMap = {
  available: { label: "可使用", class: "border-gray-900 text-gray-900" },
  used: { label: "已使用", class: "bg-gray-100 text-gray-500" },
  transferred: { label: "已轉贈", class: "bg-gray-100 text-gray-500" },
};

const badge = computed(() => statusMap[props.ticket.status]);
const canUse = computed(() => props.ticket.status === "available");
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
    <div class="grid grid-cols-2 gap-2 mt-2">
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
      <button
        type="button"
        :disabled="!canUse"
        class="py-2 text-sm font-medium rounded border transition-colors"
        :class="
          canUse
            ? 'bg-white text-gray-900 border-gray-400 hover:bg-gray-100'
            : 'bg-white text-gray-300 border-gray-200 cursor-not-allowed'
        "
        @click="emit('transfer')"
      >
        轉贈票券
      </button>
    </div>
  </div>
</template>
<style scoped></style>
