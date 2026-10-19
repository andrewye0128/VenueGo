<!--
  TopBarWeather.vue — 首頁 TopBar 的即時氣象（昱，10/18）
  用法（layouts/TopBar.vue，日期後面那條分隔線之後）：<TopBarWeather />

  1. 平常：膠囊按鈕，裡面是天氣小圖示＋現在氣溫＋小箭頭（高雄站觀測；10/18 組內決定運動中心在高雄）。
     點一下，從下方展開小視窗（跟時光機同一種 UPopover）。
  2. 小視窗：今天／明天／後天三欄，沒有分隔線；每欄是天氣圖示和最低～最高溫。
     電腦、平板比手機大一號（10/18 微調：原本太小）。
  3. 一律用真實時間，不受時光機影響：天氣預報沒辦法回到過去或跳到未來。
  4. 抓不到資料（後端沒設定授權碼、氣象署連不上）就整個不顯示，TopBar 其他東西照常。
  5. 每 10 分鐘重新抓一次（後端也快取 10 分鐘），離開頁面時停掉。
  6. 小視窗最下面寫「資料來源：中央氣象署」：政府開放資料的授權條款要求標示來源。

  10/18 微調：
  7. 看得出來可以點：
     - 一直都有：淡淡的外框、右邊的小箭頭（打開時轉向上），滑鼠移上去變白底、變藍。
     - 掃光：一道淡藍色的光從左掃到右（約 1.4 秒）。10/18 晚改成昱設計的規則：
       a. 每次重新整理、換頁（網址的路徑改變）後，隨機等 3～5 秒掃第一次。
       b. 首頁：第一次掃完之後，固定再等 10 秒掃第二次，不掃第三次（10/19 改，原本是每 90 秒一次）。
       c. 首頁以外：只掃那一次，下次換頁或重新整理才再掃。
       d. 不記錄任何東西（不用 localStorage）。
       系統設定「減少動態效果」的人完全不掃。
       「換頁」看的是路徑（route.path）：同一頁只改查詢字串（例如評論專區切換篩選）不算換頁，不然每點一次篩選就掃一次。
  8. 天氣圖示都看得到文字（圖示只分 9 種，「多雲時晴」「晴時多雲」都畫成同一個，文字才是準的）：
     - 滑鼠：移到圖示上出現提示框（TopBar 的按鈕、小視窗裡的三個圖示都有）。
     - 觸控（pointer-coarse）：沒有「移上去」這件事，所以小視窗的圖示下面直接寫出天氣文字；
       TopBar 按鈕本來就是點了打開小視窗，小視窗第一行就寫著現在的天氣，不用另外處理。
       手機以外的觸控螢幕（平板）也一樣，看的是「主要的指標是不是手指」，不是螢幕寬度。
-->
<script setup>
import { ref, computed, watch, onMounted, onBeforeUnmount } from "vue";
import { useRoute } from "vue-router";
import { getWeather } from "@/api/weatherApi";
import { weatherIcon, isNightNow } from "@/utils/weather/weatherIcon";

const REFRESH_MS = 10 * 60 * 1000;
const SHEEN_FIRST_MIN_MS = 3000; // 換頁後第一次掃光：隨機 3～5 秒（檔頭第 7 點）
const SHEEN_FIRST_MAX_MS = 5000;
const SHEEN_RUN_MS = 1400; // 掃一次的時間，要跟下面 CSS 的 animation 秒數一樣
const SHEEN_HOME_SECOND_MS = 10 * 1000; // 首頁：第一次掃完後再等 10 秒掃第二次

const weather = ref(null);
const open = ref(false);
let timer = null;

async function load() {
  // try/catch 的決定：這裡就是處理錯誤的地方。天氣只是附加資訊，
  // 抓不到就保留上一次成功的（第一次就失敗＝不顯示），不跳錯誤訊息打擾使用者。
  try {
    weather.value = await getWeather();
  } catch {
    // 不處理：維持原本的 weather
  }
}

onMounted(() => {
  load();
  timer = setInterval(load, REFRESH_MS);
});
onBeforeUnmount(() => clearInterval(timer));

// ── 掃光（檔頭第 7 點）──
// 做法（10/19 改）：每掃一次 sheenRun 加 1，掃光的 span 用它當 :key。
// key 一變，Vue 就丟掉舊的 span、換一個新的，CSS 動畫自然從頭跑一次。
// 不再靠 animationend 把狀態改回來：原本「true → 等動畫結束 → false」只要有一次沒收到 animationend，
// 狀態就卡在 true，之後再設 true 都沒有變化，就再也不掃了。改成 key 之後沒有「卡住」這種狀態。
const route = useRoute();
const sheenRun = ref(0); // 0＝這一頁還沒掃過
let firstSheenTimer = null;
let homeSecondTimer = null;

function playSheen() {
  // 分頁在背景（切到別的分頁、視窗縮小）時跳過這一次：瀏覽器不畫背景分頁，掃了也沒人看到
  if (document.visibilityState !== "visible") {
    if (import.meta.env.DEV) console.debug("[TopBarWeather] 掃光：分頁在背景，跳過這一次");
    return;
  }
  sheenRun.value += 1;
  if (import.meta.env.DEV) {
    console.debug(`[TopBarWeather] 掃光第 ${sheenRun.value} 次`, new Date().toLocaleTimeString());
  }
}

function stopSheenTimers() {
  clearTimeout(firstSheenTimer);
  clearTimeout(homeSecondTimer);
}

function scheduleSheen() {
  stopSheenTimers();
  sheenRun.value = 0;
  const delay = SHEEN_FIRST_MIN_MS + Math.random() * (SHEEN_FIRST_MAX_MS - SHEEN_FIRST_MIN_MS);
  firstSheenTimer = setTimeout(() => {
    playSheen();
    // 「是不是首頁」要在這裡（3～5 秒後）才判斷，不能在 scheduleSheen 一開始就判斷（10/19 修正）：
    // 重新整理時，元件掛上的那一刻 vue-router 還沒完成第一次導航，route 是起始位置，
    // path 是 "/"、name 還沒有值，所以會被當成「不是首頁」；等導航完成，path 還是 "/" 沒變，
    // watch 不會再觸發，首頁的第二次掃光就永遠排不進去。換頁再回首頁時沒有這個問題。
    if (route.name === "home") {
      homeSecondTimer = setTimeout(playSheen, SHEEN_RUN_MS + SHEEN_HOME_SECOND_MS);
    }
  }, delay);
}

// immediate：重新整理（元件第一次掛上）也算一次「換頁」
watch(() => route.path, scheduleSheen, { immediate: true });
onBeforeUnmount(stopSheenTimers);

const current = computed(() => weather.value?.current ?? null);
const days = computed(() => weather.value?.days ?? []);
const visible = computed(() => current.value != null || days.value.length > 0);

const pad = (n) => String(n).padStart(2, "0");
const tempText = computed(() => (current.value ? `${Math.round(current.value.temperature)}°` : ""));
const currentIcon = computed(() =>
  current.value ? weatherIcon(current.value.weather, isNightNow()) : null,
);
const observedText = computed(() => {
  if (!current.value) return "";
  const d = new Date(current.value.observedAt);
  return Number.isNaN(d.getTime()) ? "" : `${pad(d.getHours())}:${pad(d.getMinutes())} 觀測`;
});

// 最低、最高都有才寫「21°～27°」；只有一個就只寫那一個
function rangeText(d) {
  if (d.min != null && d.max != null) return `${d.min}°～${d.max}°`;
  if (d.max != null) return `${d.max}°`;
  if (d.min != null) return `${d.min}°`;
  return "—";
}

const buttonLabel = computed(() => {
  const place = weather.value?.placeName ?? "";
  if (!current.value) return `${place}天氣預報`;
  const w = current.value.weather ? `，${current.value.weather}` : "";
  return `${place}現在 ${Math.round(current.value.temperature)} 度${w}，看三天預報`;
});

// 滑鼠移到 TopBar 按鈕上的提示（檔頭第 8 點）
const buttonTip = computed(() => {
  const w = current.value?.weather;
  return w ? `現在${w}・點一下看三天預報` : "點一下看三天預報";
});
</script>

<template>
  <UPopover v-if="visible" v-model:open="open" :content="{ align: 'start' }">
    <button
      type="button"
      class="group relative isolate overflow-hidden rounded-full tabular-nums ring-1 ring-neutral-border transition-colors hover:bg-neutral-surface hover:text-brand-primary hover:ring-brand-primary/40 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
      :class="open && 'bg-neutral-surface text-brand-primary ring-brand-primary/40'"
      :aria-label="buttonLabel"
    >
      <!--
        提示框掛在裡面這層 span 上，不直接包住 button：button 已經是 UPopover 的觸發元素，
        再包一層 UTooltip 等於一個元素同時當兩個元件的觸發點，交給裡面一層比較單純。
        span 撐滿整顆按鈕（padding 在 span 上），所以滑到按鈕任何地方都會出現提示。
        小視窗打開時不出現提示（提示的內容小視窗裡都有了）。
      -->
      <UTooltip :text="buttonTip" :disabled="open">
        <span class="flex items-center gap-1 py-0.5 pr-1.5 pl-2">
          <UIcon
            v-if="currentIcon"
            :name="currentIcon"
            class="size-4 shrink-0"
            aria-hidden="true"
          />
          <span v-if="current">{{ tempText }}</span>
          <span v-else>天氣</span>
          <UIcon
            name="i-lucide-chevron-down"
            class="size-3.5 shrink-0 opacity-70 transition-transform group-hover:opacity-100"
            :class="open && 'rotate-180'"
            aria-hidden="true"
          />
        </span>
      </UTooltip>
      <!-- 掃完停在按鈕右邊外面（overflow-hidden 看不到），下一次換新的 key 才重跑 -->
      <span v-if="sheenRun > 0" :key="sheenRun" class="weather-sheen" aria-hidden="true"></span>
    </button>

    <template #content>
      <div class="p-3.5 text-base text-neutral-text-primary md:p-5 md:text-lg">
        <p class="text-sm text-neutral-text-secondary md:text-[15px]">
          {{ weather.placeName }}
          <template v-if="current">
            ・現在 {{ tempText }}<template v-if="current.weather">，{{ current.weather }}</template>
          </template>
        </p>

        <!-- 三欄、沒有分隔線（檔頭第 2 點） -->
        <div class="mt-2.5 grid grid-cols-3 gap-6 md:mt-3 md:gap-8">
          <div v-for="d in days" :key="d.date" class="flex flex-col items-center gap-1 md:gap-1.5">
            <span class="text-sm text-neutral-text-secondary md:text-base">{{ d.label }}</span>
            <UTooltip v-if="weatherIcon(d.weather, false)" :text="d.weather">
              <span class="inline-flex">
                <UIcon
                  :name="weatherIcon(d.weather, false)"
                  class="size-9 text-brand-primary md:size-11"
                  aria-hidden="true"
                />
              </span>
            </UTooltip>
            <!-- 天氣文字：滑鼠的人看提示框，這行只給螢幕閱讀器；觸控的人直接顯示（檔頭第 8 點） -->
            <span
              v-if="d.weather"
              class="sr-only text-center text-xs leading-tight text-neutral-text-secondary pointer-coarse:not-sr-only"
            >
              {{ d.weather }}
            </span>
            <span class="font-semibold whitespace-nowrap tabular-nums">{{ rangeText(d) }}</span>
          </div>
        </div>

        <p class="mt-3 text-xs text-neutral-text-secondary md:mt-4 md:text-sm">
          資料來源：中央氣象署<template v-if="observedText">・{{ observedText }}</template>
        </p>
      </div>
    </template>
  </UPopover>
</template>

<style scoped>
/* 掃光（檔頭第 7 點）：一道斜的淡藍光從按鈕左邊外面掃到右邊外面，跑一次 1.4 秒（改秒數要一起改 SHEEN_RUN_MS）。
   什麼時候掃由程式決定（scheduleSheen），這裡只管掃一次的樣子。 */
.weather-sheen {
  position: absolute;
  inset: 0 auto 0 0;
  z-index: -1;
  width: 40%;
  pointer-events: none;
  background: linear-gradient(
    90deg,
    transparent,
    color-mix(in srgb, var(--color-brand-primary) 22%, transparent),
    transparent
  );
  animation: weather-sheen 1.4s ease-in-out both;
}

@keyframes weather-sheen {
  0% {
    transform: translateX(-150%) skewX(-20deg);
  }
  100% {
    transform: translateX(300%) skewX(-20deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .weather-sheen {
    display: none;
  }
}
</style>
