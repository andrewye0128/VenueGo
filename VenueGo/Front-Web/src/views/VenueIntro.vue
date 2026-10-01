<script setup>
// 場館資訊頁：左側（手機在上方）是運動類型清單，一次只顯示選中的那一個運動
// 網址 /venues/:sportTypeId，不帶 Id 或 Id 不存在時導到第一項
import { computed, onMounted, ref, watch } from "vue";
import { RouterLink, useRoute, useRouter } from "vue-router";
import { getVenueIntroduction } from "@/api/venueApi";

const route = useRoute();
const router = useRouter();

const businessHours = ref([]);
const sportTypes = ref([]);
const isLoading = ref(true);
const errorMessage = ref("");

// 無障礙空間：全館共用，寫死在前台
const accessibilityItems = ["無障礙電梯", "無障礙廁所", "無障礙坡道", "無障礙停車場"];

// 依網址的 sportTypeId 找出選中的運動（路由參數是字串，要轉成字串比對）
const selectedSport = computed(
  () =>
    sportTypes.value.find((item) => String(item.sportTypeId) === route.params.sportTypeId) ?? null,
);

// 確保有選中的運動：網址沒帶 Id 或 Id 不在清單內時，換成第一項
// 用 replace 不留下歷史紀錄，按上一頁不會回到沒有 Id 的網址
function ensureSelection() {
  if (sportTypes.value.length === 0 || selectedSport.value) {
    return;
  }
  router.replace({ name: "venues", params: { sportTypeId: sportTypes.value[0].sportTypeId } });
}

// 注意事項：後台沒填時顯示「無」
const notice = computed(() => selectedSport.value?.notice || "無");

// 金額加千分位
function formatPrice(price) {
  return `NT$ ${price.toLocaleString()}`;
}

// 第一步：進頁面時呼叫一次 API，之後切換運動只換顯示的資料，不重新呼叫
onMounted(async () => {
  try {
    const data = await getVenueIntroduction();
    businessHours.value = data.businessHours;
    sportTypes.value = data.sportTypes;
    ensureSelection();
  } catch (error) {
    // http.js 已經把錯誤轉成中文訊息
    errorMessage.value = error.message;
  } finally {
    isLoading.value = false;
  }
});

// 第二步：在同一頁點導覽列「場館資訊」（/venues）時元件不會重建，要再檢查一次
watch(() => route.params.sportTypeId, ensureSelection);
</script>

<template>
  <main class="mx-auto max-w-7xl px-4 py-10 md:px-6">
    <h1 class="text-2xl font-bold text-neutral-text-primary">場館資訊</h1>

    <p v-if="isLoading" class="mt-8 text-sm text-neutral-text-secondary">載入中…</p>

    <div
      v-else-if="errorMessage"
      class="mt-8 rounded-lg border border-semantic-error bg-neutral-surface p-4 text-sm text-semantic-error"
    >
      {{ errorMessage }}
    </div>

    <p v-else-if="sportTypes.length === 0" class="mt-8 text-sm text-neutral-text-secondary">
      目前沒有可介紹的運動項目。
    </p>

    <!-- 手機：清單在上、內容在下；電腦（lg）：清單在左、內容在右 -->
    <div v-else class="mt-8 grid grid-cols-1 gap-8 lg:grid-cols-[240px_minmax(0,1fr)]">
      <aside>
        <nav class="rounded-lg border border-neutral-border bg-neutral-surface p-4">
          <h2 class="mb-3 text-base font-semibold text-neutral-text-primary">運動項目</h2>
          <ul class="flex flex-wrap gap-2 lg:flex-col lg:gap-1">
            <li v-for="item in sportTypes" :key="item.sportTypeId">
              <RouterLink
                :to="{ name: 'venues', params: { sportTypeId: item.sportTypeId } }"
                class="block rounded px-3 py-2 text-sm font-medium transition-colors"
                :class="
                  item.sportTypeId === selectedSport?.sportTypeId
                    ? 'bg-brand-primary text-white'
                    : 'text-neutral-text-secondary hover:bg-brand-primary/8 hover:text-brand-primary'
                "
              >
                {{ item.sportName }}
              </RouterLink>
            </li>
          </ul>
        </nav>
      </aside>

      <article
        v-if="selectedSport"
        class="rounded-lg border border-neutral-border bg-neutral-surface p-6"
      >
        <h2 class="text-xl font-bold text-neutral-text-primary">{{ selectedSport.sportName }}</h2>

        <img
          v-if="selectedSport.photoPath"
          :src="selectedSport.photoPath"
          :alt="selectedSport.sportName"
          class="mt-4 aspect-video w-full max-w-3xl rounded-md object-cover"
        />
        <div
          v-else
          class="mt-4 flex aspect-video w-full max-w-3xl items-center justify-center rounded-md border border-neutral-border bg-brand-background text-sm text-neutral-text-secondary"
        >
          尚無照片
        </div>

        <!-- 每一列：左邊藍底標籤、右邊內容；手機改成上下排列 -->
        <dl class="mt-6 divide-y divide-neutral-border border-t border-neutral-border">
          <div class="grid grid-cols-1 gap-3 py-5 md:grid-cols-[140px_minmax(0,1fr)] md:gap-6">
            <dt
              class="self-start justify-self-start rounded-md bg-brand-primary px-4 py-2 text-sm font-semibold text-white md:justify-self-stretch md:text-center"
            >
              開放時間
            </dt>
            <dd class="text-sm text-neutral-text-primary">
              <ul class="space-y-1">
                <li v-for="day in businessHours" :key="day.dayName" class="flex gap-4">
                  <span class="w-10 font-medium">{{ day.dayName }}</span>
                  <span v-if="day.isOpen" class="tabular-nums">
                    {{ day.openTime }} – {{ day.closeTime }}
                  </span>
                  <span v-else class="text-neutral-text-secondary">公休</span>
                </li>
              </ul>
            </dd>
          </div>

          <div class="grid grid-cols-1 gap-3 py-5 md:grid-cols-[140px_minmax(0,1fr)] md:gap-6">
            <dt
              class="self-start justify-self-start rounded-md bg-brand-primary px-4 py-2 text-sm font-semibold text-white md:justify-self-stretch md:text-center"
            >
              收費標準
            </dt>
            <dd class="text-sm text-neutral-text-primary">
              <!-- 沒有價格規則或規則停用時，後端回傳 price: null -->
              <p v-if="!selectedSport.price">請洽櫃台詢問</p>
              <!-- 有尖峰價：離峰、尖峰各一行，加上尖峰起始時間 -->
              <ul v-else-if="selectedSport.price.peakPrice !== null" class="space-y-1">
                <li class="tabular-nums">
                  離峰 {{ formatPrice(selectedSport.price.offPeakPrice) }}／小時
                </li>
                <li class="tabular-nums text-semantic-peak">
                  尖峰 {{ formatPrice(selectedSport.price.peakPrice) }}／小時
                </li>
                <li class="text-neutral-text-secondary">
                  尖峰起始時間：{{ selectedSport.price.peakSummary }}（至打烊）
                </li>
              </ul>
              <!-- 不分尖離峰：只有一個價格 -->
              <p v-else class="tabular-nums">
                {{ formatPrice(selectedSport.price.offPeakPrice) }}／小時
              </p>
            </dd>
          </div>

          <div class="grid grid-cols-1 gap-3 py-5 md:grid-cols-[140px_minmax(0,1fr)] md:gap-6">
            <dt
              class="self-start justify-self-start rounded-md bg-brand-primary px-4 py-2 text-sm font-semibold text-white md:justify-self-stretch md:text-center"
            >
              注意事項
            </dt>
            <!-- whitespace-pre-line 保留後台輸入的換行；不用 v-html，避免 XSS -->
            <!-- {{ }} 要緊貼標籤，否則前後的換行也會被 pre-line 顯示成空白行 -->
            <dd class="text-sm whitespace-pre-line text-neutral-text-primary">{{ notice }}</dd>
          </div>

          <div class="grid grid-cols-1 gap-3 py-5 md:grid-cols-[140px_minmax(0,1fr)] md:gap-6">
            <dt
              class="self-start justify-self-start rounded-md bg-brand-primary px-4 py-2 text-sm font-semibold text-white md:justify-self-stretch md:text-center"
            >
              無障礙空間
            </dt>
            <dd class="flex flex-wrap gap-2">
              <span
                v-for="item in accessibilityItems"
                :key="item"
                class="rounded-md border border-neutral-border px-3 py-1 text-sm text-neutral-text-primary"
              >
                {{ item }}
              </span>
            </dd>
          </div>
        </dl>
      </article>
    </div>
  </main>
</template>
