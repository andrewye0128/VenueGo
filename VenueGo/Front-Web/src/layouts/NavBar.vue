<script setup>
import { computed, onMounted, onUnmounted, ref } from "vue";
import { RouterLink, useRouter } from "vue-router";
import Logo from "@/components/Logo.vue";
import IconMenu from "@/components/icons/IconMenu.vue";
import IconCalendarPlus from "@/components/icons/IconCalendarPlus.vue";
import NavDrawer from "./NavDrawer.vue";
// 選單資料統一放在 constants/navigation.js，和 Footer 共用
import { mainNavItems as navItems } from "@/constants/navigation";
import { useMemberStore } from "@/stores/member";
const memberStore = useMemberStore();
const isDrawerOpen = ref(false);
const isDropdownOpen = ref(false);
const dropdownRef = ref(null);
const mobileDropdownRef = ref(null);
const router = useRouter();

// 處理登出邏輯
const handleLogout = async () => {
  isDropdownOpen.value = false;
  memberStore.logout();
  await router.push({ name: "login" });
};

// 點擊「會員中心」
const goToProfile = async () => {
  isDropdownOpen.value = false;
  await router.push("/member/profile");
};

// 點擊選單外部自動關閉下拉選單
const handleClickOutside = (event) => {
  if (
    dropdownRef.value &&
    !dropdownRef.value.contains(event.target) &&
    mobileDropdownRef.value &&
    !mobileDropdownRef.value.contains(event.target)
  ) {
    isDropdownOpen.value = false;
  }
};
onMounted(() => {
  document.addEventListener("click", handleClickOutside);
});

onUnmounted(() => {
  document.removeEventListener("click", handleClickOutside);
});
</script>

<template>
  <nav class="border-b border-neutral-border bg-neutral-surface">
    <!-- 手機 & 平板：☰ | Logo | 預約 (+ 已登入時的手機版小頭) -->
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

      <Logo class="h-9 justify-self-center gap-2" />

      <div class="flex items-center justify-self-end gap-2">
        <div v-if="memberStore.isLoggedIn" ref="mobileDropdownRef" class="relative">
          <button
            type="button"
            class="flex size-9 cursor-pointer items-center justify-center rounded-full bg-brand-primary text-sm font-bold text-white ring ring-neutral-border transition hover:ring-brand-primary focus:outline-none"
            aria-label="會員功能選單"
            @click="isDropdownOpen = !isDropdownOpen"
          >
            <span>{{ memberStore.member?.name?.charAt(0) }}</span>
          </button>

          <!-- 手機版彈出選單 -->
          <div
            v-if="isDropdownOpen"
            class="absolute right-0 mt-2 w-36 rounded-md border border-neutral-border bg-neutral-surface py-1 shadow-lg z-50"
          >
            <button
              type="button"
              class="flex w-full cursor-pointer items-center gap-2 px-4 py-2 text-left text-sm text-neutral-text-primary hover:bg-brand-primary/8"
              @click="goToProfile"
            >
              <UIcon name="i-mdi-card-account-details-outline" class="size-4" />
              會員中心
            </button>
            <button
              type="button"
              class="flex w-full cursor-pointer items-center gap-2 px-4 py-2 text-left text-sm text-red-600 hover:bg-red-50"
              @click="handleLogout"
            >
              <UIcon name="i-mdi-logout" class="size-4" />
              登出
            </button>
          </div>
        </div>
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

    <!-- 電腦版：Logo + 選單 | 登入/小頭 + 立即預約 -->
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
          <!-- 根據登入狀態切換：已登入顯示小頭，未登入顯示登入按鈕 -->
          <template v-if="memberStore.isLoggedIn">
            <div ref="dropdownRef" class="relative">
              <button
                type="button"
                class="flex cursor-pointer items-center gap-2 rounded-full p-1.5 transition hover:bg-brand-primary/8 focus:outline-none"
                aria-label="會員功能選單"
                @click="isDropdownOpen = !isDropdownOpen"
              >
                <div
                  class="flex size-10 items-center justify-center rounded-full bg-brand-primary text-base font-bold text-white"
                >
                  <span>{{ memberStore.member?.name?.charAt(0) }}</span>
                </div>
                <span class="text-sm font-semibold text-neutral-text-primary">
                  {{ memberStore.member?.name }}
                </span>
                <!-- <UIcon name="i-mdi-chevron-down" class="size-4 text-neutral-text-secondary" /> -->
              </button>

              <!-- 電腦版彈出選單 -->
              <div
                v-if="isDropdownOpen"
                class="absolute right-0 mt-2 w-40 rounded-md border border-neutral-border bg-neutral-surface py-1 shadow-lg z-50"
              >
                <button
                  type="button"
                  class="flex cursor-pointer w-full items-center gap-2 px-4 py-2 text-left text-sm text-neutral-text-primary hover:bg-brand-primary/8"
                  @click="goToProfile"
                >
                  <UIcon name="i-mdi-card-account-details-outline" class="size-4" />
                  會員中心
                </button>
                <button
                  type="button"
                  class="flex cursor-pointer w-full items-center gap-2 px-4 py-2 text-left text-sm text-red-600 hover:bg-red-50"
                  @click="handleLogout"
                >
                  <UIcon name="i-mdi-logout" class="size-4" />
                  登出
                </button>
              </div>
            </div>
          </template>
          <template v-else>
            <UButton color="primary" variant="outline" size="lg" to="/login">登入</UButton>
          </template>
          <UButton color="action" size="lg" to="/booking">立即預約</UButton>
        </div>
      </div>
    </div>

    <NavDrawer :open="isDrawerOpen" :items="navItems" @close="isDrawerOpen = false" />
  </nav>
</template>
