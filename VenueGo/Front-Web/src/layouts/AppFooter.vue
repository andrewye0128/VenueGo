<script setup>
import { RouterLink } from "vue-router";
import { footerLinkGroups } from "@/constants/navigation";
import IconMapPin from "@/components/icons/IconMapPin.vue";
import IconPhone from "@/components/icons/IconPhone.vue";
import IconClock from "@/components/icons/IconClock.vue";
import IconClockSolid from "@/components/icons/IconClockSolid.vue";

// 場館聯絡資訊：之後若改由後端提供，只要替換這個陣列
// icon 直接放元件，平日用空心時鐘、假日用實心時鐘，對應設計稿
const contactItems = [
  { icon: IconMapPin, label: "場館地址", value: "高雄市中正區運動路 100 號" },
  // href 用國際格式：+886 台灣國碼，區碼 02 去掉開頭的 0
  { icon: IconPhone, label: "聯絡電話", value: "(02) 1234-5678", href: "tel:+886212345678" },
  { icon: IconClock, label: "平日開放時間", value: "06:00-22:00" },
  { icon: IconClockSolid, label: "假日開放時間", value: "08:00-21:00" },
];

// 手機版折疊選單的資料：UAccordion 用 label 當標題，links 放在 #body 插槽裡顯示
const accordionItems = footerLinkGroups.map((group) => ({
  label: group.title,
  links: group.links,
}));

// UAccordion 預設是淺色背景用的樣式，Footer 是深色背景，所以只在這裡調整（不改全站設定）
const accordionUi = {
  root: "border-t border-white/10",
  item: "border-white/10",
  trigger: "py-3.5 text-base font-bold text-white",
  body: "pb-4",
};

// 年份自動更新，不用每年回來改
const currentYear = new Date().getFullYear();
</script>

<template>
  <footer class="bg-brand-dark text-sm text-white/75">
    <div class="mx-auto max-w-7xl px-4 py-10 md:px-6">
      <!-- 手機：上下排列 / 平板：資訊 | 3 組連結 左右並排 / 電腦：4 欄 -->
      <div
        class="grid gap-8 md:grid-cols-[auto_1fr] md:gap-12 xl:grid-cols-[2fr_1fr_1fr_1fr] xl:gap-8"
      >
        <div>
          <h2 class="mb-4 text-base font-bold text-white">VenueGo 運動中心</h2>
          <address class="space-y-2 not-italic">
            <!-- 平板寬度較窄，不換行讓地址維持一行 -->
            <p
              v-for="item in contactItems"
              :key="item.label"
              class="flex items-center gap-2 md:whitespace-nowrap xl:whitespace-normal"
            >
              <component :is="item.icon" class="h-4 w-4 shrink-0 text-white" />
              <span>
                {{ item.label }}：
                <a
                  v-if="item.href"
                  :href="item.href"
                  class="tabular-nums transition-colors hover:text-white"
                >
                  {{ item.value }}
                </a>
                <span v-else class="tabular-nums">{{ item.value }}</span>
              </span>
            </p>
          </address>
        </div>

        <!-- 手機：折疊選單，一次只能打開一組（type 預設為 single） -->
        <UAccordion :items="accordionItems" :ui="accordionUi" class="md:hidden">
          <template #body="{ item }">
            <nav :aria-label="item.label">
              <ul class="space-y-2">
                <li v-for="link in item.links" :key="link.to">
                  <RouterLink :to="link.to" class="transition-colors hover:text-white">
                    {{ link.label }}
                  </RouterLink>
                </li>
              </ul>
            </nav>
          </template>
        </UAccordion>

        <!-- 平板、電腦：一律展開
             平板時這層是 3 欄的 grid；電腦時用 contents 讓這層「消失」，3 組連結直接成為外層 4 欄的後 3 欄 -->
        <div class="hidden md:grid md:grid-cols-3 md:gap-8 xl:contents">
          <nav v-for="group in footerLinkGroups" :key="group.title" :aria-label="group.title">
            <h2 class="mb-4 text-base font-bold text-white">{{ group.title }}</h2>
            <ul class="space-y-2">
              <li v-for="link in group.links" :key="link.to">
                <RouterLink :to="link.to" class="transition-colors hover:text-white">
                  {{ link.label }}
                </RouterLink>
              </li>
            </ul>
          </nav>
        </div>
      </div>
    </div>

    <div class="border-t border-white/10">
      <p class="mx-auto max-w-7xl px-4 py-4 text-center text-xs text-white/60 md:px-6">
        © {{ currentYear }} VenueGo 運動中心股份有限公司　All rights reserved.
      </p>
    </div>
  </footer>
</template>
