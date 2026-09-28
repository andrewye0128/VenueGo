<script setup>
import { ref } from "vue";
import { RouterLink } from "vue-router";
import Logo from "@/components/Logo.vue";
import IconMenu from "@/components/icons/IconMenu.vue";
import IconCalendarPlus from "@/components/icons/IconCalendarPlus.vue";
import NavDrawer from "./NavDrawer.vue";
// 選單資料統一放在 constants/navigation.js，和 Footer 共用
import { mainNavItems as navItems } from "@/constants/navigation";

const isDrawerOpen = ref(false);
</script>

<template>
  <nav class="border-b border-neutral-border bg-neutral-surface">
    <!-- 手機 & 平板：☰ | Logo | 預約 -->
    <div class="grid h-16 grid-cols-3 items-center px-4 md:px-6 xl:hidden">
      <button
        type="button"
        class="cursor-pointer justify-self-start rounded p-2 text-neutral-text-primary transition hover:bg-brand-primary/8 active:bg-brand-primary/16"
        aria-label="開啟選單"
        :aria-expanded="isDrawerOpen"
        @click="isDrawerOpen = true"
      >
        <IconMenu class="h-6 w-6" />
      </button>

      <Logo class="h-9 justify-self-center" />

      <div class="justify-self-end">
        <!-- 手機：只有 icon -->
        <div class="md:hidden">
          <!-- 圖示沿用 IconCalendarPlus，放在 UButton 裡面，外觀跟原本一樣 -->
          <UButton color="action" to="/booking" square aria-label="立即預約">
            <IconCalendarPlus class="h-6 w-6" />
          </UButton>
        </div>
        <!-- 平板：文字按鈕 -->
        <div class="hidden md:block">
          <UButton color="action" to="/booking">立即預約</UButton>
        </div>
      </div>
    </div>

    <!-- 電腦版：Logo + 選單 | 登入 + 立即預約 -->
    <div class="mx-auto hidden h-20 max-w-7xl items-center justify-center px-6 xl:flex">
      <div class="flex w-full h-full justify-between items-center gap-10">
        <Logo class="h-12" />

        <ul class="flex h-full items-center gap-8">
          <li v-for="item in navItems" :key="item.to" class="h-full">
            <RouterLink
              :to="item.to"
              class="flex h-full items-center border-b-2 border-transparent font-semibold text-neutral-text-secondary transition-colors hover:text-brand-primary"
              active-class="!border-brand-primary !text-brand-primary"
            >
              {{ item.label }}
            </RouterLink>
          </li>
        </ul>

        <div class="flex items-center gap-4">
          <UButton color="primary" variant="outline" size="lg" to="/login">登入</UButton>
          <UButton color="action" size="lg" to="/booking">立即預約</UButton>
        </div>
      </div>

      <!-- <div class="flex items-center gap-4">
        <BaseButton variant="secondary" size="lg" to="/login">登入</BaseButton>
        <BaseButton variant="action" size="lg" to="/booking">立即預約</BaseButton>
      </div> -->
    </div>

    <NavDrawer :open="isDrawerOpen" :items="navItems" @close="isDrawerOpen = false" />
  </nav>
</template>
