<!--
    ReviewIndexView.vue — 評論專區（取代 Views/CReview/Index.cshtml；導覽列「會員評價」連到這裡）
    路由：/reviews?sportTypeId=&range=&star=&hasContentOnly=&sort=

    ── 篩選條件放在網址上，不是放在元件的變數裡 ─────────────
    Razor 版的設計理由沿用：顧客端的網址要能分享、能加書籤、能按上一頁。
    所以這一頁的「唯一事實來源」是網址的查詢字串：
      使用者點篩選 → router.push 改網址 → 監聽到網址變了 → 重新向後端要資料
    按瀏覽器上一頁也是走同一條路，不需要另外處理。

    ── 選項與預設值都由後端給 ───────────────────────────────
    時間範圍有哪些選項、預設是「一年內」、排序預設「評分最高」，
    這些都在 ReviewIndexVM.cs 的 ReviewRange／ReviewSort 裡，後端隨資料一起回傳。
    前端不自己記一份，後端改了預設值這裡自動跟著改（同一個值只存在一個地方）。
-->
<script setup>
import { ref, computed, watch, nextTick } from "vue";
import { useRoute, useRouter, RouterLink } from "vue-router";
import { getPublicReviews } from "@/api/reviewApi";
import StarView from "@/components/review/StarView.vue";
import ReviewCard from "@/components/review/ReviewCard.vue";

const route = useRoute();
const router = useRouter();

const vm = ref(null); // 後端回傳的整份資料（規格 2-1）
const loading = ref(false);
const error = ref(null);

// ── 網址 → 送給後端的條件 ──
//  只挑認得的欄位，其餘忽略。值對不對交給後端判斷（後端本來就會把不認得的值換回預設）。
function filterFromQuery(q) {
  const f = {};
  if (q.sportTypeId) f.sportTypeId = q.sportTypeId;
  if (q.range) f.range = q.range;
  if (q.star) f.star = q.star;
  // 'true'／'false' 兩種都明確送出；網址上沒寫（例如直接打 /reviews）才交給後端補預設值
  if (q.hasContentOnly === "true" || q.hasContentOnly === "false")
    f.hasContentOnly = q.hasContentOnly === "true";
  if (q.sort) f.sort = q.sort;
  return f;
}

// ── 條件 → 網址 ──
//  以「後端確認過的目前條件」為底，只改傳進來的那幾個。
//  傳 null 代表「清掉這個條件」（例如取消星等篩選）。
//
//  ⚠️ 時間範圍、排序、只看有留言，這三個「一律寫進網址」，就算是預設值也寫。
//     （2026-09-26 修正：第一版把「等於預設值的」省略掉，讓後端落到「不認得 → 用預設」那一支。）
//     省略的問題：後端哪天改了預設值，舊的分享連結意思就變了——
//     使用者分享的是「一年內」，對方打開看到的卻是新預設的「一個月內」。
//     網址上寫齊，連結的意思就固定了，不必依賴後端的「否則」。
//
//     運動類型、星等是另一回事：它們的「沒有值」本身就是一個明確的狀態（＝不篩選），
//     後端是用「有沒有值」判斷，不是落到「否則」，所以沒有值時不寫。
function queryFor(changes = {}) {
  const merged = { ...vm.value.filter, ...changes };
  const q = {};
  if (merged.sportTypeId != null) q.sportTypeId = String(merged.sportTypeId);
  q.range = merged.range;
  if (merged.star != null) q.star = String(merged.star);
  q.hasContentOnly = merged.hasContentOnly ? "true" : "false";
  q.sort = merged.sort;
  return q;
}

// 「清除全部篩選」：把每個條件明確設回後端給的預設值，而不是給一個空網址讓後端自己補
function resetLink() {
  const d = vm.value.defaults;
  return linkFor({
    sportTypeId: null,
    range: d.range,
    star: null,
    hasContentOnly: false,
    sort: d.sort,
  });
}

// 網址上缺了哪個條件（例如使用者直接打 /reviews），載入後把後端整理好的條件補回網址。
// 用 replace 不是 push：這不是使用者的操作，不該多一筆上一頁紀錄。
function sameQuery(a, b) {
  const ka = Object.keys(a),
    kb = Object.keys(b);
  return ka.length === kb.length && ka.every((k) => String(a[k]) === String(b[k]));
}

function linkFor(changes) {
  return { name: "review-index", query: queryFor(changes) };
}

// ── 載入 ──
//  使用者連續快速點好幾個篩選時，回應回來的順序不一定跟送出的順序一樣。
//  用流水號記住「最後一次送出的是哪一次」，比較早的回應回來了就丟掉，
//  畫面才不會被舊的結果蓋掉。
let seq = 0;
const listVersion = ref(0); // 每次換資料就 +1，卡片的 key 會跟著變 → 展開狀態重置
let skipNextLoad = false; // 「補齊網址」本身也會改網址，那一次不必重新載入（資料已經是對的）

async function load() {
  const mine = ++seq;
  loading.value = true;
  error.value = null;
  try {
    const data = await getPublicReviews(filterFromQuery(route.query));
    if (mine !== seq) return;
    vm.value = data;
    listVersion.value++;
    nextTick(revealActiveTab);

    const full = queryFor();
    if (!sameQuery(full, route.query)) {
      skipNextLoad = true;
      router.replace({ name: "review-index", query: full });
    }
  } catch (e) {
    if (mine !== seq) return;
    error.value = e.message;
  } finally {
    if (mine === seq) loading.value = false;
  }
}

watch(
  () => route.query,
  () => {
    if (skipNextLoad) {
      skipNextLoad = false;
      return;
    }
    load();
  },
  { immediate: true },
);

// ── 運動類型分頁：手機上是可以左右滑的一排 ──
//  從分享連結進來時，選中的分頁可能在畫面右邊外面，使用者看不出「現在篩的是哪一項」。
//  所以每次載入完，把選中的分頁捲到這一排的中間。
//  ⚠️ 不用 el.scrollIntoView()：它在分頁不在畫面內時會連整頁一起捲上去
//     （例如使用者在頁面下方改了排序），這裡只捲「這一排」的水平位置。
const tabNav = ref(null);
function revealActiveTab() {
  const nav = tabNav.value;
  if (!nav) return;
  const el = nav.querySelector('[aria-current="page"]');
  if (!el) return;
  nav.scrollTo({ left: el.offsetLeft - (nav.clientWidth - el.offsetWidth) / 2 });
}

// ── 畫面用的衍生值 ──
const hasFilter = computed(() => {
  if (!vm.value) return false;
  const f = vm.value.filter,
    d = vm.value.defaults;
  return (
    f.sportTypeId != null ||
    f.star != null ||
    f.hasContentOnly ||
    f.range !== d.range ||
    f.sort !== d.sort
  );
});

// ── 星等分布的每一條 ──
const barActive = (bar) => vm.value.filter.star === bar.star;
const barDisabled = (bar) => bar.count === 0 && !barActive(bar);
const barLink = (bar) => linkFor({ star: barActive(bar) ? null : bar.star }); // 再點一次＝取消
const barTitle = (bar) =>
  barActive(bar)
    ? "再點一次取消篩選"
    : bar.count === 0
      ? "沒有這個星等的評論"
      : `只看 ${bar.star} 星的評論`;

// 下拉選單與開關：改了就換網址（USelect 的選項格式是 { label, value }，後端給的是 { value, text }）
const rangeItems = computed(() =>
  vm.value.options.ranges.map((o) => ({ label: o.text, value: o.value })),
);
const sortItems = computed(() =>
  vm.value.options.sorts.map((o) => ({ label: o.text, value: o.value })),
);
const range = computed({
  get: () => vm.value?.filter.range,
  set: (v) => router.push(linkFor({ range: v })),
});
const sort = computed({
  get: () => vm.value?.filter.sort,
  set: (v) => router.push(linkFor({ sort: v })),
});
const hasContentOnly = computed({
  get: () => vm.value?.filter.hasContentOnly ?? false,
  set: (v) => router.push(linkFor({ hasContentOnly: v })),
});
</script>

<template>
  <main class="mx-auto max-w-7xl px-4 py-10 md:px-6">
    <h1 class="mb-6 text-2xl font-bold text-neutral-text-primary">評論專區</h1>

    <!-- 第一次載入（還沒有任何資料） -->
    <p v-if="!vm && loading" class="py-10 text-center text-neutral-text-secondary">載入中⋯</p>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      icon="i-lucide-circle-alert"
      :title="error"
      :actions="[{ label: '重試', color: 'error', variant: 'outline', onClick: load }]"
      class="mb-4"
    />

    <template v-if="vm">
      <!-- ── 運動類型分頁 ──
           手機放不下所有分頁時改成「一排、可左右滑」（overflow-x-auto ＋ 每個分頁 shrink-0 不折字）。
           底部那條灰線畫在 nav 自己的底框；選中的分頁用自己的 2px 底框蓋上去（-mb-px）。
           可捲動的容器會裁掉超出的內容，所以底線用 border-b 畫在 nav 上，分頁不必超出去。 -->
      <nav
        ref="tabNav"
        aria-label="運動類型"
        class="mb-4 flex overflow-x-auto overscroll-x-contain border-b border-neutral-border"
      >
        <RouterLink
          v-for="tab in vm.sportTabs"
          :key="tab.sportTypeId ?? 'all'"
          :to="linkFor({ sportTypeId: tab.sportTypeId })"
          :aria-current="vm.filter.sportTypeId === tab.sportTypeId ? 'page' : undefined"
          class="shrink-0 border-b-2 px-4 py-2 text-sm whitespace-nowrap focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-brand-primary pointer-coarse:py-3"
          :class="
            vm.filter.sportTypeId === tab.sportTypeId
              ? 'border-brand-primary font-semibold text-neutral-text-primary'
              : 'border-transparent text-brand-primary hover:bg-brand-primary/8'
          "
        >
          {{ tab.name }}
        </RouterLink>
      </nav>

      <!-- ── 摘要與星等分布 ── -->
      <section
        class="mb-4 grid gap-6 rounded-lg border border-neutral-border bg-neutral-surface p-4 md:grid-cols-3 md:p-6"
      >
        <div class="flex flex-col items-center justify-center text-center">
          <div class="text-5xl leading-none font-bold text-neutral-text-primary">
            {{ vm.summary.average.toFixed(1) }}
          </div>
          <StarView :rating="Math.round(vm.summary.average)" size-class="mt-2 text-lg" />
          <div class="mt-2 text-sm text-neutral-text-secondary">
            共 {{ vm.summary.total }} 則評論
          </div>
        </div>

        <div class="md:col-span-2">
          <!-- 「看起來可以點」要同時給三個訊號：外觀、文字、狀態。這行字是成本最低的那一個。
               圖示用漏斗（篩選），不用向上指的手：向上的手指容易被讀成「往上看」，而不是「可以點」（9/28） -->
          <div
            class="mb-2 flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-neutral-text-secondary"
          >
            <span class="inline-flex items-center gap-1">
              <UIcon name="i-lucide-funnel" aria-hidden="true" />
              點選星等可以只看該分數的評論
            </span>
            <UButton
              v-if="vm.filter.star != null"
              :to="linkFor({ star: null })"
              color="primary"
              variant="ghost"
              size="sm"
              icon="i-lucide-circle-x"
              label="取消星等篩選"
            />
          </div>

          <!-- 每一條都是連結；只有「筆數為 0 而且不是目前選中的」改用 div，
               因為點了只會得到空清單，那是挫折不是功能。
               <component :is> 讓同一段內容可以依情況變成 RouterLink 或 div，不必寫兩份。 -->
          <component
            :is="barDisabled(bar) ? 'div' : RouterLink"
            v-for="bar in vm.summary.bars"
            :key="bar.star"
            v-bind="barDisabled(bar) ? { 'aria-disabled': 'true' } : { to: barLink(bar) }"
            :aria-current="barActive(bar) ? 'true' : undefined"
            :title="barTitle(bar)"
            class="flex items-center gap-2 rounded px-2 py-1 focus-visible:outline-2 focus-visible:outline-brand-primary pointer-coarse:min-h-11"
            :class="[
              barActive(bar) ? 'bg-brand-primary/8 ring-1 ring-brand-primary ring-inset' : '',
              barDisabled(bar) ? 'opacity-45' : 'hover:bg-brand-primary/8',
            ]"
          >
            <span class="w-12 shrink-0 text-sm text-neutral-text-primary">{{ bar.star }} 星</span>
            <span
              class="h-2.5 flex-1 overflow-hidden rounded-full bg-brand-background"
              aria-hidden="true"
            >
              <span
                class="block h-full rounded-full bg-rating-star"
                :style="{ width: bar.percent + '%' }"
              ></span>
            </span>
            <span class="w-10 shrink-0 text-right text-sm text-neutral-text-secondary">{{
              bar.count
            }}</span>
          </component>
        </div>
      </section>

      <!-- ── 篩選列 ── -->
      <div class="mb-4 flex flex-wrap items-end gap-x-4 gap-y-3">
        <UFormField label="時間">
          <USelect v-model="range" :items="rangeItems" class="w-36" />
        </UFormField>

        <UFormField label="排序">
          <USelect v-model="sort" :items="sortItems" class="w-36" />
        </UFormField>

        <!-- h-10：跟旁邊的下拉選單同高、底部對齊（只調位置，不改開關本身的樣式） -->
        <USwitch v-model="hasContentOnly" label="只看有留言的" class="h-10" />

        <UButton
          v-if="hasFilter"
          :to="resetLink()"
          color="primary"
          variant="outline"
          label="清除全部篩選"
        />
      </div>

      <!-- ── 卡片 ── -->
      <!-- 換篩選時先讓舊清單變淡，不要整塊清空再長回來，畫面才不會閃 -->
      <div :class="loading ? 'opacity-50 transition-opacity' : ''" :aria-busy="loading">
        <div
          v-if="vm.items.length === 0"
          class="rounded-lg border border-neutral-border bg-neutral-surface py-12 text-center text-neutral-text-secondary"
        >
          <UIcon name="i-lucide-message-square-text" class="mx-auto mb-2 size-10" aria-hidden="true" />
          <template v-if="hasFilter">
            <div>目前的條件沒有找到評論</div>
            <div class="mt-1 text-sm">可以放寬時間範圍，或按「清除全部篩選」</div>
          </template>
          <template v-else>這裡還沒有公開的評論</template>
        </div>

        <!-- 一行太長不好讀，限制寬度（208 × 4px = 832px，沿用 Razor 版的 52rem） -->
        <div v-else class="flex max-w-208 flex-col gap-3">
          <!-- ⚠️ 卡片資料刻意不含 reviewId（連號整數，不能外流），
               所以 key 用「第幾次載入 + 第幾張」。每次換資料都會整批重建，
               上一批「展開全文」的狀態不會殘留到下一批。 -->
          <ReviewCard v-for="(item, i) in vm.items" :key="`${listVersion}-${i}`" :item="item" />
        </div>
      </div>
    </template>
  </main>
</template>
