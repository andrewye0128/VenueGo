<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { RouterLink, useRoute, useRouter } from "vue-router";
import Logo from "@/components/Logo.vue";
import IconMenu from "@/components/icons/IconMenu.vue";
import IconCalendarPlus from "@/components/icons/IconCalendarPlus.vue";
import NavDrawer from "./NavDrawer.vue";
import MemberMenuPanel from "./MemberMenuPanel.vue";
// 選單資料統一放在 constants/navigation.js，和 Footer 共用
import { mainNavItems as navItems } from "@/constants/navigation";
import { useMemberStore } from "@/stores/member";
const memberStore = useMemberStore();
const isDrawerOpen = ref(false);
const isDropdownOpen = ref(false);
const dropdownRef = ref(null);
const mobileDropdownRef = ref(null);
const router = useRouter();
const route = useRoute();

// 登入按鈕的目的地（電腦版、手機＆平板版、側邊選單共用）：
// 帶上 ?redirect=目前網址，登入成功後 LoginView 會帶回原本的頁面（網址是否安全由 LoginView 檢查）
// 以下情況不帶 redirect：
//   - 已經在登入頁、註冊頁：帶了會變成「登入完回到登入頁」
//   - 首頁：沒帶的話 LoginView 本來就會回首頁，網址比較乾淨
const NO_REDIRECT_ROUTES = ["home", "login", "register"];
const loginTo = computed(() =>
  NO_REDIRECT_ROUTES.includes(route.name)
    ? { name: "login" }
    : { name: "login", query: { redirect: route.fullPath } },
);

// ── 會員選單的開關 ──
// 手機＆平板：點頭像才打開、再點一次關閉（toggleMobileDropdown）
// 電腦版：滑鼠移到頭像上就打開、移開就關閉；點一下會「固定」打開，移開也不關（點外面或再點一次才關）
//   為什麼點擊不是單純切換：滑鼠移上去時選單已經打開了，點下去如果切換，選單反而會被關掉
// 移開時等一下才關：頭像和選單中間有 8px 的空隙（mt-2），滑鼠經過空隙時不能馬上關掉
const HOVER_CLOSE_DELAY_MS = 150;
const isDropdownPinned = ref(false);
let hoverCloseTimer = null;

const closeDropdown = () => {
  clearTimeout(hoverCloseTimer);
  isDropdownOpen.value = false;
  isDropdownPinned.value = false;
};

const toggleMobileDropdown = () => {
  if (isDropdownOpen.value) closeDropdown();
  else isDropdownOpen.value = true;
};

// pointerType 只認滑鼠：用手指點觸控螢幕的電腦時，不要被當成「滑過」
const openOnHover = (event) => {
  if (event.pointerType !== "mouse") return;
  clearTimeout(hoverCloseTimer);
  isDropdownOpen.value = true;
};

const closeOnLeave = (event) => {
  if (event.pointerType !== "mouse" || isDropdownPinned.value) return;
  hoverCloseTimer = setTimeout(closeDropdown, HOVER_CLOSE_DELAY_MS);
};

const toggleDesktopPin = () => {
  if (isDropdownPinned.value) {
    closeDropdown();
    return;
  }
  clearTimeout(hoverCloseTimer);
  isDropdownOpen.value = true;
  isDropdownPinned.value = true;
};

// 處理登出邏輯
const handleLogout = async () => {
  closeDropdown();
  memberStore.logout();
  await router.push({ name: "login" });
};

// 換頁就關閉選單：選單裡的連結不用各自處理，按瀏覽器的上一頁換頁時也會關掉
watch(() => route.fullPath, closeDropdown);

// 點擊選單外部自動關閉下拉選單
const handleClickOutside = (event) => {
  if (
    dropdownRef.value &&
    !dropdownRef.value.contains(event.target) &&
    mobileDropdownRef.value &&
    !mobileDropdownRef.value.contains(event.target)
  ) {
    closeDropdown();
  }
};
onMounted(() => {
  document.addEventListener("click", handleClickOutside);
});

onUnmounted(() => {
  document.removeEventListener("click", handleClickOutside);
  clearTimeout(hoverCloseTimer);
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
            :aria-expanded="isDropdownOpen"
            @click="toggleMobileDropdown"
          >
            <span>{{ memberStore.member?.name?.charAt(0) }}</span>
          </button>

          <!-- 手機版彈出選單（內容跟電腦版共用 MemberMenuPanel） -->
          <MemberMenuPanel
            v-if="isDropdownOpen"
            :name="memberStore.member?.name"
            :email="memberStore.member?.email"
            class="absolute right-0 z-50 mt-2"
            @logout="handleLogout"
          />
        </div>
        <!-- 未登入：放在頭像的同一個位置，登入、登出時畫面不會跳動
             顏色跟電腦版一樣（外框的 primary），跟旁邊實心的「立即預約」區分主次
             手機右邊那一格太窄（320px 手機約 96px），只放圖示；平板放文字
             圖示用 Lucide：線條粗細、圓角跟旁邊的 IconCalendarPlus 一致 -->
        <template v-else>
          <UButton
            color="primary"
            variant="outline"
            :to="loginTo"
            square
            icon="i-lucide-user-round"
            aria-label="登入"
            class="md:hidden"
          />
          <UButton color="primary" variant="outline" :to="loginTo" class="hidden md:inline-flex">
            登入
          </UButton>
        </template>
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
            <!-- 滑過的事件放在外層：滑鼠從頭像移到選單上時，還在外層裡面，選單不會關掉 -->
            <div
              ref="dropdownRef"
              class="relative"
              @pointerenter="openOnHover"
              @pointerleave="closeOnLeave"
            >
              <button
                type="button"
                class="flex cursor-pointer items-center gap-2 rounded-full p-1.5 transition hover:bg-brand-primary/8 focus:outline-none"
                aria-label="會員功能選單"
                :aria-expanded="isDropdownOpen"
                @click="toggleDesktopPin"
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

              <!-- 電腦版彈出選單（內容跟手機版共用 MemberMenuPanel） -->
              <MemberMenuPanel
                v-if="isDropdownOpen"
                :name="memberStore.member?.name"
                :email="memberStore.member?.email"
                class="absolute right-0 z-50 mt-2"
                @logout="handleLogout"
              />
            </div>
          </template>
          <template v-else>
            <UButton color="primary" variant="outline" size="lg" :to="loginTo">登入</UButton>
          </template>
          <UButton color="action" size="lg" to="/booking">立即預約</UButton>
        </div>
      </div>
    </div>

    <NavDrawer
      :open="isDrawerOpen"
      :items="navItems"
      :is-logged-in="memberStore.isLoggedIn"
      :login-to="loginTo"
      @close="isDrawerOpen = false"
    />
  </nav>
</template>
