<script setup>
import { ref, computed, onMounted, onUnmounted } from "vue";
import { RouterLink } from "vue-router";
import IconUsers from "@/components/icons/IconUsers.vue";
import IconCalendar from "@/components/icons/IconCalendar.vue";

defineProps({
  // 目前先寫死，之後改接後端 API
  onlineCount: { type: Number, default: 86 },
});

const now = ref(new Date());
let timer = null;

onMounted(() => {
  timer = setInterval(() => (now.value = new Date()), 1000);
});
// 元件移除時清掉計時器，避免在背景一直執行
onUnmounted(() => clearInterval(timer));

const WEEKDAYS = ["日", "一", "二", "三", "四", "五", "六"];
const pad = (n) => String(n).padStart(2, "0");

const time = computed(() => `${pad(now.value.getHours())}:${pad(now.value.getMinutes())}`);
const monthDay = computed(() => `${pad(now.value.getMonth() + 1)}/${pad(now.value.getDate())}`);

// 平板：09/24 19:05
const shortDateTime = computed(() => `${monthDay.value} ${time.value}`);
// 電腦：2026/09/24 (四) 19:05
const fullDateTime = computed(
  () =>
    `${now.value.getFullYear()}/${monthDay.value} (${WEEKDAYS[now.value.getDay()]}) ${time.value}`,
);
</script>

<template>
  <div
    class="border-b border-neutral-border bg-brand-background text-sm text-neutral-text-secondary"
  >
    <div class="mx-auto flex h-9 max-w-7xl items-center justify-between px-4 md:px-6">
      <div class="flex items-center gap-3">
        <span class="flex items-center gap-1.5">
          <IconUsers class="h-4 w-4" />
          <span class="hidden xl:inline">站內</span>
          <span class="tabular-nums">{{ onlineCount }} 人</span>
        </span>

        <!-- 手機版不顯示日期 -->
        <span class="hidden h-4 w-px bg-neutral-border md:block"></span>
        <span class="hidden items-center gap-1.5 tabular-nums md:flex">
          <IconCalendar class="h-4 w-4" />
          <span class="xl:hidden">{{ shortDateTime }}</span>
          <span class="hidden xl:inline">{{ fullDateTime }}</span>
        </span>
      </div>

      <div class="flex items-center gap-3">
        <RouterLink to="/login" class="transition-colors hover:text-brand-primary">登入</RouterLink>
        <span class="h-4 w-px bg-neutral-border"></span>
        <RouterLink to="/register" class="transition-colors hover:text-brand-primary">
          註冊
        </RouterLink>
      </div>
    </div>
  </div>
</template>
