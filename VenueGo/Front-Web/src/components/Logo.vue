<script setup>
import { RouterLink } from "vue-router";
import logo from "@/assets/images/logo/logo.svg";
</script>

<template>
  <!-- 大小由使用端的 class 控制，例如 <Logo class="h-12" />，圖片會跟著撐滿高度 -->
  <RouterLink to="/" class="inline-block" aria-label="VenueGo 首頁">
    <img :src="logo" alt="VenueGo" class="h-full w-auto" />
  </RouterLink>
</template>
