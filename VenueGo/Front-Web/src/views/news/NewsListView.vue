<script setup>
// 最新消息列表頁 /news：分類頁籤 + 圖卡（每頁 9 則）+ 換頁
// 目前的分類、頁碼放在網址上（/news?category=event&page=2）：
//   重新整理、分享網址、從詳細頁按上一頁回來，都會停在同一個分類與頁碼
// UButton、UPagination、USkeleton 由 Nuxt UI 自動匯入
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { getNewsList } from "@/api/newsApi";
import { newsCategories, isNewsCategory } from "@/constants/newsCategories";
import NewsCard from "@/components/news/NewsCard.vue";

// 每頁筆數：桌機一排 3 張 × 3 排（規格第二十節）
const PAGE_SIZE = 9;

const route = useRoute();
const router = useRouter();

// 從網址讀出目前的分類與頁碼
// 分類不在清單內（網址被亂改）就當作「全部」(null)；頁碼不是正整數就當作第 1 頁
const currentCategory = computed(() =>
  isNewsCategory(route.query.category) ? route.query.category : null,
);
const currentPage = computed(() => {
  const page = Number(route.query.page);
  return Number.isInteger(page) && page > 0 ? page : 1;
});

const result = ref(null);
const isLoading = ref(true);
const errorMessage = ref("");

async function loadNews() {
  isLoading.value = true;
  errorMessage.value = "";
  try {
    result.value = await getNewsList({
      category: currentCategory.value ?? undefined,
      page: currentPage.value,
      pageSize: PAGE_SIZE,
    });
  } catch (error) {
    // http.js 已經把錯誤轉成中文訊息
    errorMessage.value = error.message;
  } finally {
    isLoading.value = false;
  }
}

// 網址的分類或頁碼改變時重新查詢；immediate：進頁面時也查一次
watch(() => [currentCategory.value, currentPage.value], loadNews, { immediate: true });

// 切換分類：換成新的分類，頁碼回到第 1 頁（不帶 page）
function selectCategory(category) {
  router.push({ query: category ? { category } : {} });
}

// 換頁：保留目前的分類，並捲回頁面頂端
// （同一頁只換查詢字串時，router 的 scrollBehavior 不會捲動）
function changePage(page) {
  const query = { ...route.query, page };
  router.push({ query });
  window.scrollTo({ top: 0, behavior: "smooth" });
}
</script>

<template>
  <main class="mx-auto max-w-6xl px-4 py-10 md:px-6">
    <h1 class="text-2xl font-bold text-neutral-text-primary">最新消息</h1>

    <!-- 分類頁籤：選中的實心、其他外框（跟首頁場地介紹的篩選一致）；有「全部」並預設停在「全部」 -->
    <div class="mt-6 flex flex-wrap gap-2">
      <UButton
        color="primary"
        size="sm"
        :variant="currentCategory === null ? 'solid' : 'outline'"
        :aria-pressed="currentCategory === null"
        @click="selectCategory(null)"
      >
        全部
      </UButton>
      <UButton
        v-for="category in newsCategories"
        :key="category.value"
        color="primary"
        size="sm"
        :variant="currentCategory === category.value ? 'solid' : 'outline'"
        :aria-pressed="currentCategory === category.value"
        @click="selectCategory(category.value)"
      >
        {{ category.label }}
      </UButton>
    </div>

    <!-- 載入中：灰色卡片骨架，張數跟一頁一樣 -->
    <div v-if="isLoading" class="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <USkeleton v-for="n in PAGE_SIZE" :key="n" class="aspect-4/5 rounded-lg" />
    </div>

    <p
      v-else-if="errorMessage"
      class="mt-6 rounded-lg border border-semantic-error bg-neutral-surface p-4 text-sm text-semantic-error"
    >
      最新消息暫時無法載入：{{ errorMessage }}
    </p>

    <p v-else-if="result.items.length === 0" class="mt-6 text-sm text-neutral-text-secondary">
      這個分類目前沒有消息
    </p>

    <template v-else>
      <div class="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <NewsCard v-for="news in result.items" :key="news.id" :news="news" />
      </div>

      <!-- 換頁：總筆數超過一頁的筆數才顯示（API 只回傳總筆數，總頁數由 UPagination 自己算）；頁碼用後端修正過的 result.page（網址頁碼超過總頁數時會退回最後一頁） -->
      <div v-if="result.totalCount > result.pageSize" class="mt-8 flex justify-center">
        <UPagination
          :page="result.page"
          :total="result.totalCount"
          :items-per-page="result.pageSize"
          :sibling-count="1"
          color="primary"
          variant="outline"
          active-color="primary"
          active-variant="solid"
          @update:page="changePage"
        />
      </div>
    </template>
  </main>
</template>
