<!--
  Header 頭像的下拉選單內容：手機＆平板、電腦版共用（NavBar.vue）
  最上面顯示目前登入的會員（名字、Email），接著是會員中心的頁面，最下面是登出
  - 開關、位置由 NavBar 控制，這裡只負責「選單裡面長什麼樣子」
  - 會員中心的頁面來自 constants/navigation.js 的 memberNavItems（跟會員中心左側選單、Footer 共用）
  - 點頁面連結不用自己關選單：NavBar 換頁時會自動關閉
  UIcon 由 Nuxt UI 自動匯入
-->
<script setup>
import { RouterLink } from "vue-router";
import { memberNavItems } from "@/constants/navigation";

defineProps({
  name: { type: String, default: "" },
  email: { type: String, default: "" },
});
// 登出要清掉登入資料、換頁，由 NavBar 處理
const emit = defineEmits(["logout"]);
</script>

<template>
  <div class="w-56 rounded-md border border-neutral-border bg-neutral-surface py-1 shadow-lg">
    <!-- 目前登入的會員：手機版頭像只有一個字，看不出是哪個帳號，這裡寫清楚
         Email 太長時後面變成「…」（truncate），不會把選單撐開；滑過會顯示完整的 Email（title） -->
    <div class="border-b border-neutral-border px-4 pt-2 pb-3">
      <p class="truncate text-sm font-semibold text-neutral-text-primary">{{ name }}</p>
      <p class="truncate text-xs text-neutral-text-secondary" :title="email">{{ email }}</p>
    </div>

    <!-- 用 RouterLink（不是 button＋router.push）：可以按中鍵開新分頁、左下角看得到網址，
         目前所在的頁面會套用 active-class，用藍色標示
         aria-label 不用「會員中心」：會員中心左側選單（MemberLayout）已經用了，同一頁有兩個一樣的名稱會分不出來 -->
    <nav aria-label="會員中心頁面" class="py-1">
      <RouterLink
        v-for="item in memberNavItems"
        :key="item.to"
        :to="item.to"
        class="flex items-center gap-2 px-4 py-2 text-sm text-neutral-text-primary hover:bg-brand-primary/8"
        active-class="bg-brand-primary/8 font-semibold !text-brand-primary"
      >
        <UIcon :name="item.icon" class="size-4 shrink-0" />
        {{ item.label }}
      </RouterLink>
    </nav>

    <!-- 登出跟上面的頁面分開：不是換頁而是一個動作，用分隔線隔開比較不會誤按 -->
    <div class="border-t border-neutral-border pt-1">
      <button
        type="button"
        class="flex w-full cursor-pointer items-center gap-2 px-4 py-2 text-left text-sm text-red-600 hover:bg-red-50"
        @click="emit('logout')"
      >
        <UIcon name="i-mdi-logout" class="size-4 shrink-0" />
        登出
      </button>
    </div>
  </div>
</template>
