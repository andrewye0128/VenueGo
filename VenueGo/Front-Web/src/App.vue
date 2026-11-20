<script setup>
import { RouterView } from "vue-router";
import AppHeader from "@/layouts/AppHeader.vue";
import AppFooter from "@/layouts/AppFooter.vue";
import NavigationLoading from "@/components/NavigationLoading.vue";
import { onMounted, watch } from "vue";
import { useMemberStore } from "@/stores/member";
import { setMemberNotificationsEnabled } from "@/composables/useMemberNotifications";

const memberStore = useMemberStore();

// 會員即時通知（例如預約逾期）：登入中就連上後端，登出就斷線（說明見 useMemberNotifications.js）
// immediate：一打開網站就依目前的登入狀態處理；之後登入、登出時會再呼叫一次
watch(() => memberStore.isLoggedIn, setMemberNotificationsEnabled, { immediate: true });

// 網站打開時，恢復目前登入會員的資料（右上角才會顯示會員名字）
// 登入失效會自動登出；後端暫時不能回答的話不登出，token 留著，下次換頁再確認
// 不用 await：結果不用在這裡處理，拿到資料後畫面會自動更新
onMounted(() => {
  memberStore.ensureSession();
});
</script>

<template>
  <!-- 【POC】Nuxt UI 要求最外層包 UApp（提供 Toast、Tooltip 等功能），UApp 為自動匯入 -->
  <UApp>
    <!-- 換頁停頓時的進度條、「正在確認登入狀態」提示 -->
    <NavigationLoading />
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
