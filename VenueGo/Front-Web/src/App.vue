<script setup>
import { RouterView } from "vue-router";
import AppHeader from "@/layouts/AppHeader.vue";
import AppFooter from "@/layouts/AppFooter.vue";
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
