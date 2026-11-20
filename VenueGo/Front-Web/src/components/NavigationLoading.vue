<script setup>
// 換頁停頓時的提示（放在 App.vue，整個網站共用）
//   換頁超過 0.3 秒 → 頁面最上方出現跑動的進度條（不擋住畫面）
//   而且正在確認登入狀態 → 蓋住整個畫面，正中間顯示「正在確認登入狀態，請稍候…」
//     確認完之前，底下的按鈕都按不到，避免客人在等待時又按了別的東西
// 什麼時候算「換頁中」見 composables/useNavigationLoading.js
// UIcon 由 Nuxt UI 自動匯入
import { computed } from "vue";
import { useNavigationLoading } from "@/composables/useNavigationLoading";
import { useMemberStore } from "@/stores/member";

const { isLoading } = useNavigationLoading();
const memberStore = useMemberStore();

// 換頁卡住的原因是「正在確認登入狀態」時，才蓋住畫面、說明在等什麼
const showSessionMessage = computed(() => isLoading.value && memberStore.checkingSession);
</script>

<template>
  <!-- 進度條：固定在最上方，比下面的遮罩高一層；pointer-events-none 不擋住底下的按鈕 -->
  <div
    v-if="isLoading"
    class="pointer-events-none fixed inset-x-0 top-0 z-[70] h-1 overflow-hidden bg-brand-primary/20"
    role="progressbar"
    aria-label="頁面載入中"
  >
    <div class="navigation-loading-bar h-full w-1/3 bg-brand-primary" />
  </div>

  <!-- 確認登入狀態中：半透明遮罩蓋住整個畫面（包含頁首），正中間說明在等什麼
       z-[60]：比頁首、彈出視窗（z-50）高，才蓋得住 -->
  <div
    v-if="showSessionMessage"
    class="fixed inset-0 z-[60] flex items-center justify-center bg-white/70 px-4 backdrop-blur-sm"
    role="status"
    aria-live="polite"
  >
    <div
      class="flex items-center gap-3 rounded-md border border-neutral-border bg-neutral-surface px-5 py-4 text-sm text-neutral-text-primary shadow-lg"
    >
      <UIcon name="i-lucide-loader-circle" class="size-5 animate-spin text-brand-primary" />
      <span>正在確認登入狀態，請稍候…</span>
    </div>
  </div>
</template>

<style scoped>
/* 進度條從左跑到右，一直重複（不知道要等多久，所以不顯示百分比） */
.navigation-loading-bar {
  animation: navigation-loading 1.2s ease-in-out infinite;
}

@keyframes navigation-loading {
  from {
    transform: translateX(-100%);
  }
  to {
    transform: translateX(300%);
  }
}

/* 系統設定「減少動態效果」的人：不跑動，改成整條淡淡地閃 */
@media (prefers-reduced-motion: reduce) {
  .navigation-loading-bar {
    width: 100%;
    animation: navigation-loading-fade 1.5s ease-in-out infinite;
  }

  @keyframes navigation-loading-fade {
    50% {
      opacity: 0.4;
    }
  }
}
</style>
