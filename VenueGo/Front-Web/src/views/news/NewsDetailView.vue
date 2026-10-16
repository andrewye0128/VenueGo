<script setup>
// 最新消息詳細頁 /news/:id：封面、分類、發布日期、標題、完整內文、上一篇／下一篇
// UBadge、UButton、UIcon、USkeleton 由 Nuxt UI 自動匯入
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { getNewsDetail } from "@/api/newsApi";

const route = useRoute();

const news = ref(null);
const isLoading = ref(true);
const errorMessage = ref("");
// 找不到這則消息（不存在、已刪除、還沒發布）：跟其他錯誤分開顯示
const isNotFound = ref(false);

// 後端回傳 2026-10-13T15:50:00，詳細頁只顯示日期 2026/10/13
const publishedDate = computed(() =>
  news.value ? news.value.publishedAt.slice(0, 10).replaceAll("-", "/") : "",
);

async function loadNews(id) {
  isLoading.value = true;
  errorMessage.value = "";
  isNotFound.value = false;
  try {
    news.value = await getNewsDetail(id);
  } catch (error) {
    // 後端回 404 時，http.js 會帶上後端的錯誤代碼
    if (error.errorCode === "NEWS_NOT_FOUND") {
      isNotFound.value = true;
    } else {
      errorMessage.value = error.message;
    }
  } finally {
    isLoading.value = false;
  }
}

// 點「上一篇／下一篇」時只有網址的 id 改變，vue-router 會沿用同一個頁面元件，不會重新掛載
// 所以要監看 id，改變時重新查詢；immediate：進頁面時也查一次
watch(() => route.params.id, loadNews, { immediate: true });
</script>

<template>
  <main class="mx-auto max-w-4xl px-4 py-10 md:px-6">
    <UButton color="primary" variant="link" leading-icon="i-mdi-chevron-left" to="/news">
      返回最新消息
    </UButton>

    <!-- 載入中：封面與文字的骨架 -->
    <div v-if="isLoading" class="mt-6 flex flex-col gap-6 md:flex-row">
      <USkeleton class="aspect-4/5 w-full rounded-lg md:w-1/3" />
      <div class="flex flex-1 flex-col gap-3">
        <USkeleton class="h-6 w-1/3" />
        <USkeleton class="h-8 w-2/3" />
        <USkeleton class="h-40 w-full" />
      </div>
    </div>

    <!-- 找不到：不存在、已刪除、還沒發布 -->
    <div v-else-if="isNotFound" class="mt-10 flex flex-col items-center text-center">
      <h1 class="text-2xl font-bold text-neutral-text-primary">找不到這則消息</h1>
      <p class="mt-2 text-sm text-neutral-text-secondary">這則消息可能已經下架，或網址有誤</p>
      <UButton color="primary" class="mt-6" to="/news">回到最新消息列表</UButton>
    </div>

    <p
      v-else-if="errorMessage"
      class="mt-6 rounded-lg border border-semantic-error bg-neutral-surface p-4 text-sm text-semantic-error"
    >
      消息暫時無法載入：{{ errorMessage }}
    </p>

    <article v-else class="mt-6">
      <!-- 手機：封面在上、文字在下；平板以上：封面在左（佔 1/3）、文字在右 -->
      <div class="flex flex-col gap-6 md:flex-row">
        <img
          :src="news.imagePath"
          :alt="news.title"
          class="aspect-4/5 w-full rounded-lg object-cover md:w-1/3"
        />

        <div class="flex flex-1 flex-col gap-3">
          <div class="flex flex-wrap items-center gap-2">
            <!-- 分類統一 primary 色，用文字區分（前台規則不新增顏色） -->
            <UBadge color="primary" variant="subtle">{{ news.categoryName }}</UBadge>
            <time :datetime="news.publishedAt" class="text-sm text-neutral-text-secondary">
              {{ publishedDate }}
            </time>
          </div>

          <h1 class="text-2xl font-bold text-neutral-text-primary">{{ news.title }}</h1>

          <!-- 內文是純文字：whitespace-pre-line 保留後台輸入的換行；用 {{ }} 顯示，不用 v-html，避免 XSS -->
          <p class="leading-relaxed whitespace-pre-line text-neutral-text-primary">
            {{ news.content }}
          </p>
        </div>
      </div>

      <!-- 上一篇／下一篇：依發布時間找前後一則；沒有就不顯示那一側 -->
      <nav
        aria-label="上一篇與下一篇消息"
        class="mt-10 grid gap-4 border-t border-neutral-border pt-6 sm:grid-cols-2"
      >
        <div>
          <RouterLink
            v-if="news.previous"
            :to="{ name: 'news-detail', params: { id: news.previous.id } }"
            class="group flex flex-col gap-1 rounded-lg p-2 hover:bg-neutral-surface focus-visible:outline-2 focus-visible:outline-brand-primary"
          >
            <span class="flex items-center gap-1 text-sm text-neutral-text-secondary">
              <UIcon name="i-mdi-chevron-left" class="size-4" />
              上一篇
            </span>
            <span class="line-clamp-2 text-neutral-text-primary group-hover:text-brand-primary">
              {{ news.previous.title }}
            </span>
          </RouterLink>
        </div>
        <div>
          <RouterLink
            v-if="news.next"
            :to="{ name: 'news-detail', params: { id: news.next.id } }"
            class="group flex flex-col gap-1 rounded-lg p-2 text-right hover:bg-neutral-surface focus-visible:outline-2 focus-visible:outline-brand-primary"
          >
            <span class="flex items-center justify-end gap-1 text-sm text-neutral-text-secondary">
              下一篇
              <UIcon name="i-mdi-chevron-right" class="size-4" />
            </span>
            <span class="line-clamp-2 text-neutral-text-primary group-hover:text-brand-primary">
              {{ news.next.title }}
            </span>
          </RouterLink>
        </div>
      </nav>
    </article>
  </main>
</template>
