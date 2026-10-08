<script setup>
// 首頁「場地介紹」區塊：運動類型篩選 + 場地圖卡單列輪播
// UCarousel、UButton、USkeleton 由 Nuxt UI 自動匯入
import { computed, onMounted, ref } from "vue";
import { getVenues } from "@/api/venueApi";
import VenueCard from "@/components/venues/VenueCard.vue";
import IconMapPin from "@/components/icons/IconMapPin.vue";
import IconChevronLeftBold from "@/components/icons/IconChevronLeftBold.vue";
import IconChevronRightBold from "@/components/icons/IconChevronRightBold.vue";

const venues = ref([]);
const isLoading = ref(true);
const errorMessage = ref("");

// 目前選中的運動類型；null 代表「全部」
const selectedSportTypeId = ref(null);

// 篩選選項：從場地資料整理出「有場地的運動類型」，順序依 API 回傳（已依運動類型排序）
const sportTypeOptions = computed(() => {
  const options = new Map();
  for (const venue of venues.value) {
    if (!options.has(venue.sportTypeId)) {
      options.set(venue.sportTypeId, venue.sportTypeName);
    }
  }
  return [...options].map(([id, name]) => ({ id, name }));
});

// 依篩選條件顯示的場地
const filteredVenues = computed(() =>
  selectedSportTypeId.value === null
    ? venues.value
    : venues.value.filter((venue) => venue.sportTypeId === selectedSportTypeId.value),
);

// 進頁面時呼叫一次 API，切換篩選只在前端過濾，不重新呼叫
onMounted(async () => {
  try {
    venues.value = await getVenues();
  } catch (error) {
    // http.js 已經把錯誤轉成中文訊息
    errorMessage.value = error.message;
  } finally {
    isLoading.value = false;
  }
});

// 只有這個區塊長這樣，寫在這裡的 :ui，不改全站設定
const carouselUi = {
  // 卡片等高
  container: "items-stretch",
  // 一次顯示幾張：手機 85%（露出下一張邊緣，提示可以滑動）、平板 2 張、電腦 4 張
  item: "basis-[85%] md:basis-1/2 lg:basis-1/4",
  // 手機用滑的，不顯示箭頭；左右兩個箭頭都不能按（張數不超過一頁）時整組隱藏
  arrows: "max-md:hidden has-[[data-slot=prev]:disabled]:has-[[data-slot=next]:disabled]:hidden",
  // 箭頭放在卡片內側（預設在外側，會超出版面）
  prev: "start-2 sm:start-2 rounded text-neutral-text-primary",
  next: "end-2 sm:end-2 rounded text-neutral-text-primary",
};
</script>

<template>
  <section>
    <!-- 區塊標題：h2（首頁的 h1 在 HomeView） -->
    <div class="flex items-center gap-2">
      <IconMapPin class="size-6 text-brand-accent" />
      <h2 class="text-2xl font-bold text-neutral-text-primary">場地介紹</h2>
    </div>

    <!-- 載入中：灰色卡片骨架，張數跟輪播一樣（手機 1、平板 2、電腦 4） -->
    <div v-if="isLoading" class="mt-6 grid gap-4 md:grid-cols-2 lg:grid-cols-4">
      <USkeleton
        v-for="n in 4"
        :key="n"
        class="aspect-3/4 rounded-lg"
        :class="{ 'hidden md:block': n === 2, 'hidden lg:block': n > 2 }"
      />
    </div>

    <!-- 載入失敗：只影響這個區塊，首頁其他區塊照常顯示 -->
    <p
      v-else-if="errorMessage"
      class="mt-6 rounded-lg border border-semantic-error bg-neutral-surface p-4 text-sm text-semantic-error"
    >
      場地資訊暫時無法載入：{{ errorMessage }}
    </p>

    <p v-else-if="venues.length === 0" class="mt-6 text-sm text-neutral-text-secondary">
      目前沒有可預約的場地
    </p>

    <template v-else>
      <!-- 運動類型篩選：選中的實心、其他外框 -->
      <div class="mt-4 flex flex-wrap gap-2">
        <UButton
          color="primary"
          size="sm"
          :variant="selectedSportTypeId === null ? 'solid' : 'outline'"
          :aria-pressed="selectedSportTypeId === null"
          @click="selectedSportTypeId = null"
        >
          全部
        </UButton>
        <UButton
          v-for="option in sportTypeOptions"
          :key="option.id"
          color="primary"
          size="sm"
          :variant="selectedSportTypeId === option.id ? 'solid' : 'outline'"
          :aria-pressed="selectedSportTypeId === option.id"
          @click="selectedSportTypeId = option.id"
        >
          {{ option.name }}
        </UButton>
      </div>

      <!-- :key：切換篩選時重建輪播，回到第一張 -->
      <!-- align="start"：第一張靠左對齊；slides-to-scroll="auto"：按一次箭頭換一整頁 -->
      <UCarousel
        :key="selectedSportTypeId ?? 'all'"
        v-slot="{ item }"
        :items="filteredVenues"
        align="start"
        slides-to-scroll="auto"
        arrows
        :prev-icon="IconChevronLeftBold"
        :next-icon="IconChevronRightBold"
        :ui="carouselUi"
        class="mt-6"
      >
        <VenueCard :venue="item" />
      </UCarousel>
    </template>
  </section>
</template>
