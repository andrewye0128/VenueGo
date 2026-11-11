<script setup>
import { RouterView } from "vue-router";
import AppHeader from "@/layouts/AppHeader.vue";
import AppFooter from "@/layouts/AppFooter.vue";
import { onMounted, watch } from "vue";
import { useMemberStore } from "@/stores/member";
import { setMemberNotificationsEnabled } from "@/composables/useMemberNotifications";

const memberStore = useMemberStore();

// 會員即時通知（例如預約逾期）：登入中就連上後端，登出就斷線（說明見 useMemberNotifications.js）
// immediate：一打開網站就依目前的登入狀態處理；之後登入、登出時會再呼叫一次
watch(() => memberStore.isLoggedIn, setMemberNotificationsEnabled, { immediate: true });

// 網站啟動時，恢復目前登入會員的資料
onMounted(async () => {
  const token = localStorage.getItem("token");

  // 沒有 Token，就不需要載入會員資料
  if (!token) return;

  // Pinia 尚未取得會員資料時，向後端確認登入狀態
  if (!memberStore.member) {
    try {
      await memberStore.fetchCurrentMember();
    } catch (error) {
      // Token 無效或過期時，清除登入狀態
      memberStore.logout();
    }
  }
});
</script>

<template>
  <!-- 【POC】Nuxt UI 要求最外層包 UApp（提供 Toast、Tooltip 等功能），UApp 為自動匯入 -->
  <UApp>
    <!-- 內容少時，flex-1 會撐開中間區塊，把 Footer 推到畫面最底下 -->
    <div class="flex min-h-screen flex-col">
      <AppHeader />
      <div class="flex-1">
        <RouterView />
      </div>
      <AppFooter />
    </div>
  </UApp>
</template>
