<script setup>
import { RouterView } from "vue-router";
import AppHeader from "@/layouts/AppHeader.vue";
import AppFooter from "@/layouts/AppFooter.vue";
import { onMounted } from "vue";
import { useMemberStore } from "@/stores/member";

const memberStore = useMemberStore();

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
