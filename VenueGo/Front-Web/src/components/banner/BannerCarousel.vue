<script setup>
// 首頁輪播圖：UCarousel 只放照片（箭頭、圓點、淡入淡出、自動播放都交給它），
// 文字區塊放在 UCarousel 外面，依「目前第幾張」切換內容。
// 這樣三種尺寸只要改變文字區塊的位置：手機在照片下方、平板在照片上方、電腦疊在照片左側。
// UCarousel、UButton 由 Nuxt UI 自動匯入
import { ref, computed, watch, onBeforeUnmount } from "vue";
import banner01 from "@/assets/images/banner/banner-01.webp";
import banner02 from "@/assets/images/banner/banner-02.webp";
import banner03 from "@/assets/images/banner/banner-03.webp";
import IconChevronLeftBold from "@/components/icons/IconChevronLeftBold.vue";
import IconChevronRightBold from "@/components/icons/IconChevronRightBold.vue";

// 3 張的按鈕都一樣（立即預約、查看場地），寫在 template 裡
const slides = [
  {
    image: banner01,
    subtitle: "運動・讓生活更精彩",
    title: "立即預約你的運動場地",
    description: "快速查詢場地、時段與價格\n和朋友一起，享受運動的每一刻",
  },
  {
    image: banner02,
    subtitle: "團隊・一起享受比賽",
    title: "和朋友一起打場好球",
    description: "簡單預約你的排球場地\n揪團、組隊，享受運動的樂趣",
  },
  {
    image: banner03,
    subtitle: "活力・體驗全新運動",
    title: "一起來打皮克球",
    description: "快速找到適合你的運動場地\n體驗全新運動，享受運動的樂趣",
  },
];

// ── 目前顯示第幾張 ──
// UCarousel 對外提供 emblaApi（底層 Embla 輪播的控制器），監聽它的 select 事件，換張時更新 currentIndex
const carousel = ref(null);
const currentIndex = ref(0);
const current = computed(() => slides[currentIndex.value]);

const onSelect = (api) => {
  currentIndex.value = api.selectedScrollSnap();
  // 不管是自動換張、按箭頭還是點圓點，換張後都重新計時 5 秒，
  // 避免剛按完箭頭，自動播放又馬上換下一張
  api.plugins().autoplay?.reset();
};
let emblaApi = null;
watch(
  () => carousel.value?.emblaApi,
  (api) => {
    emblaApi?.off("select", onSelect);
    emblaApi = api;
    emblaApi?.on("select", onSelect);
  },
  { immediate: true },
);
onBeforeUnmount(() => emblaApi?.off("select", onSelect));

// 使用者在電腦設定了「減少動態效果」時，不自動播放
const prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
// 每 5 秒換一張；滑鼠移到輪播圖上時暫停；按箭頭、圓點之後繼續自動播放
// rootNode：「滑鼠移上去暫停」預設只看照片區，箭頭和圓點不在裡面；
// 改成看照片區的外層，滑鼠停在箭頭、圓點上時也會暫停
const autoplay = prefersReducedMotion
  ? false
  : {
      delay: 5000,
      stopOnMouseEnter: true,
      stopOnInteraction: false,
      rootNode: (viewport) => viewport.parentElement,
    };

// 平板、手機的說明文字只顯示第一行（設計稿如此）
const firstLine = (text) => text.split("\n")[0];

// 只有首頁 banner 長這樣，所以寫在這裡的 :ui，不改全站設定
const carouselUi = {
  // 拿掉 UCarousel 預設的左右間距（給「一次顯示多張」用的）
  container: "ms-0",
  item: "ps-0",
  // 箭頭：放在照片內側、垂直置中，方形白底，圖示用 Neutral / Text / Primary（#1A2230）
  prev: "start-3 sm:start-3 rounded text-neutral-text-primary",
  next: "end-3 sm:end-3 rounded text-neutral-text-primary",
  // 圓點：放在照片底部；電腦版往上移，避開壓在照片下緣的查詢卡片
  dots: "bottom-3 xl:bottom-24",
  dot: "size-2.5 bg-neutral-surface ring ring-neutral-border data-[state=active]:bg-brand-primary data-[state=active]:ring-brand-primary",
};
</script>

<template>
  <section class="relative">
    <div class="flex flex-col">
      <!-- 文字區塊：手機在照片下方（order-2）、平板在照片上方（md:order-1）、電腦疊在照片左側（xl:absolute） -->
      <!-- 電腦版用 pointer-events-none，滑鼠才能穿過文字區塊去拖曳照片；按鈕再開回 pointer-events-auto -->
      <div class="order-2 md:order-1 xl:pointer-events-none xl:absolute xl:inset-0 xl:z-10">
        <div
          class="mx-auto flex max-w-7xl flex-col px-4 py-6 md:px-6 md:pt-10 md:pb-8 xl:h-full xl:justify-center xl:px-20 xl:py-0"
        >
          <!-- 換張時文字淡入淡出；aria-live 讓螢幕閱讀器唸出新的標題 -->
          <Transition
            mode="out-in"
            enter-active-class="transition-opacity duration-500"
            enter-from-class="opacity-0"
            leave-active-class="transition-opacity duration-300"
            leave-to-class="opacity-0"
          >
            <div :key="currentIndex" aria-live="polite">
              <p class="hidden text-base text-neutral-text-secondary xl:block">
                {{ current.subtitle }}
              </p>
              <!-- 標題用 h2：一頁只能有一個 h1（首頁的 h1 在 HomeView） -->
              <h2
                class="text-2xl font-bold text-neutral-text-primary md:text-3xl xl:mt-3 xl:text-5xl xl:leading-tight"
              >
                {{ current.title }}
              </h2>
              <p
                class="mt-2 text-sm text-neutral-text-secondary md:mt-3 md:text-base xl:mt-4 xl:text-lg"
              >
                <span class="xl:hidden">{{ firstLine(current.description) }}</span>
                <!-- whitespace-pre-line：讓 \n 換行 -->
                <span class="hidden whitespace-pre-line xl:inline">{{ current.description }}</span>
              </p>
            </div>
          </Transition>

          <!-- 按鈕：3 張都一樣，不跟著淡入淡出；手機不顯示 -->
          <div class="mt-6 hidden gap-4 md:flex xl:pointer-events-auto xl:mt-8">
            <UButton color="action" size="lg" to="/booking" trailing-icon="i-lucide-arrow-right">
              立即預約
            </UButton>
            <UButton color="primary" variant="outline" size="lg" to="/venues">查看場地</UButton>
          </div>
        </div>
      </div>

      <!-- 照片輪播：手機滿版、平板是有框線的區塊、電腦滿版 -->
      <div
        class="order-1 md:order-2 md:mx-auto md:w-full md:max-w-7xl md:px-6 xl:max-w-none xl:px-0"
      >
        <UCarousel
          ref="carousel"
          v-slot="{ item }"
          :items="slides"
          arrows
          dots
          loop
          fade
          :autoplay="autoplay"
          :prev-icon="IconChevronLeftBold"
          :next-icon="IconChevronRightBold"
          :ui="carouselUi"
          class="md:overflow-hidden md:rounded md:border md:border-neutral-border xl:rounded-none xl:border-0"
        >
          <!-- 圖片只是背景，重要資訊都在文字區塊，所以 alt 留空讓螢幕閱讀器略過 -->
          <!-- 手機、平板照片較窄，靠右顯示球場主體（照片左側是給電腦版放文字的白色漸層） -->
          <img
            :src="item.image"
            alt=""
            class="h-64 w-full object-cover object-right md:h-80 xl:h-130 xl:object-center"
          />
        </UCarousel>
      </div>
    </div>
  </section>
</template>
