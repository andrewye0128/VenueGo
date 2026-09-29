<script setup>
import { RouterLink } from "vue-router";
// 換 logo 檔只要改這一行
import logo from "@/assets/images/logo/logo.png";
</script>

<template>
  <!-- 大小由使用端的 class 控制，例如 <Logo class="h-12" />，圖片會跟著撐滿高度 -->
  <RouterLink to="/" class="inline-block" aria-label="VenueGo 首頁">
    <img :src="logo" alt="VenueGo" class="h-full w-auto" />
  </RouterLink>
</template>
