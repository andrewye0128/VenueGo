<script setup>
// 原本：import { ref, computed, onMounted, onUnmounted } from "vue";
import { ref, computed } from "vue";
import { RouterLink } from "vue-router";
import IconUsers from "@/components/icons/IconUsers.vue";
import IconCalendar from "@/components/icons/IconCalendar.vue";
import IconClock from "@/components/icons/IconClock.vue";
import ConfirmModal from "@/components/ConfirmModal.vue";
import { useSiteClock } from "@/composables/useSiteClock";

defineProps({
  // 目前先寫死，之後改接後端 API
  onlineCount: { type: Number, default: 86 },
});

// ── 原本的寫法（昱 10/8：計時器搬進 useSiteClock，元件移除時它會自己清掉）──
// const now = ref(new Date());
// let timer = null;
//
// onMounted(() => {
//   timer = setInterval(() => (now.value = new Date()), 1000);
// });
// // 元件移除時清掉計時器，避免在背景一直執行
// onUnmounted(() => clearInterval(timer));

// 現在時間：開發時跟著後端的時光機走，正式版本就是瀏覽器的時鐘（說明見 useSiteClock.js）
const { now, available, traveling, offsetText, realNow, openPanel, resetToNow } = useSiteClock();

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

// ── 開發用時光機（只有 npm run dev、後端有開時才會出現）────────────
// 點時間 → 小面板：網站時間、真實時間、「調整時間」（開時光機小視窗）、「回到現在」

/** 2026/09/24 (四) 19:05:30，小面板用 */
const withSeconds = (d) =>
  `${d.getFullYear()}/${pad(d.getMonth() + 1)}/${pad(d.getDate())} (${WEEKDAYS[d.getDay()]}) ` +
  `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;

const panelOpen = ref(false);
const resetError = ref("");
const confirmModal = useOverlay().create(ConfirmModal);

function adjust() {
  panelOpen.value = false;
  openPanel();
}

async function confirmReset() {
  panelOpen.value = false;
  resetError.value = "";
  const choice = await confirmModal.open({
    title: "回到現在？",
    description:
      `網站時間會回到真實時間 ${withSeconds(realNow.value)}。` +
      "會留在這一頁；頁面上已經算好的內容，要重新整理才會更新。",
    actions: [
      { value: "cancel", label: "取消", variant: "outline" },
      { value: "reset", label: "回到現在" },
    ],
  });
  if (choice !== "reset") return;

  try {
    await resetToNow();
  } catch (e) {
    // try/catch 的決定：失敗時重新打開小面板，把原因寫在裡面，可以再按一次
    resetError.value = e.message || "回到現在失敗，請再試一次";
    panelOpen.value = true;
  }
}
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

        <!-- 手機版不顯示日期（時光機旅行中例外，要讓人知道現在不是真的時間） -->
        <!-- 原本：<span class="hidden h-4 w-px bg-neutral-border md:block"></span> -->
        <span
          class="h-4 w-px bg-neutral-border"
          :class="traveling ? 'block' : 'hidden md:block'"
        ></span>

        <!-- 一般情況（正式版本、後端沒開）：跟原本完全一樣 -->
        <span v-if="!available" class="hidden items-center gap-1.5 tabular-nums md:flex">
          <IconCalendar class="h-4 w-4" />
          <span class="xl:hidden">{{ shortDateTime }}</span>
          <span class="hidden xl:inline">{{ fullDateTime }}</span>
        </span>

        <!-- 開發時：時間可以點，打開時光機小面板；旅行中變成黃底 -->
        <UPopover v-else v-model:open="panelOpen" :content="{ align: 'start' }">
          <button
            type="button"
            class="items-center gap-1.5 rounded-full tabular-nums transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
            :class="
              traveling
                ? 'flex bg-semantic-warning/10 px-2 py-0.5 font-medium text-semantic-warning ring-1 ring-semantic-warning/40 hover:bg-semantic-warning/15'
                : 'hidden hover:text-brand-primary md:flex'
            "
            :title="traveling ? `時光機啟動中：比真實時間${offsetText}` : '時光機（開發用）'"
            :aria-label="
              traveling
                ? `時光機啟動中，網站時間 ${fullDateTime}`
                : `現在時間 ${fullDateTime}，打開時光機`
            "
          >
            <IconClock v-if="traveling" class="h-4 w-4" />
            <IconCalendar v-else class="h-4 w-4" />
            <span class="xl:hidden">{{ shortDateTime }}</span>
            <span class="hidden xl:inline">{{ fullDateTime }}</span>
            <span v-if="traveling" class="hidden lg:inline">・{{ offsetText }}</span>
          </button>

          <template #content>
            <div class="w-72 p-3 text-sm text-neutral-text-primary">
              <p class="text-xs text-neutral-text-secondary">
                {{ traveling ? "網站時間（時光機啟動中）" : "網站時間" }}
              </p>
              <p class="text-base font-semibold tabular-nums">
                {{ withSeconds(now) }}
              </p>
              <p v-if="traveling" class="mt-1 text-xs text-neutral-text-secondary tabular-nums">
                真實時間 {{ withSeconds(realNow) }}<br />
                <span class="font-medium text-semantic-warning">比真實時間{{ offsetText }}</span>
              </p>
              <p v-else class="mt-1 text-xs text-neutral-text-secondary">
                沒有時光旅行，就是真實時間。
              </p>

              <p v-if="resetError" class="mt-2 text-xs text-semantic-error" role="alert">
                {{ resetError }}
              </p>

              <div class="mt-3 flex gap-2">
                <UButton size="sm" variant="outline" label="調整時間" @click="adjust" />
                <UButton v-if="traveling" size="sm" label="回到現在" @click="confirmReset" />
              </div>
              <p class="mt-2 text-xs text-neutral-text-secondary">
                開發環境限定。只改後端的時間（ITimeService.Now）和這裡的顯示，前台其他用 new Date()
                的地方不會變。
              </p>
            </div>
          </template>
        </UPopover>
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
