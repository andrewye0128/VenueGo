<script setup>
// 最新消息圖卡：封面、置頂與分類標籤、發布日期、標題、摘要；整張卡片點了進詳細頁
// 首頁最新消息區塊、最新消息列表頁共用
// UBadge 由 Nuxt UI 自動匯入
import { computed } from "vue";

const props = defineProps({
  // 資料格式同 GET /api/news 的 data.items 單筆：
  // { id, title, isPinned, category, categoryName, publishedAt, imagePath, summary }
  news: { type: Object, required: true },
  // 封面圖框比例與填滿方式：預設 4:5 填滿（列表頁）；首頁輪播傳 1:1 襯框（見 HomeNewsSection）
  // 要傳完整的 class 字串，Tailwind 才掃得到
  coverClass: { type: String, default: "aspect-4/5 object-cover" },
});

// 後端回傳 2026-10-13T15:50:00，圖卡只顯示日期 2026/10/13
const publishedDate = computed(() => props.news.publishedAt.slice(0, 10).replaceAll("-", "/"));
</script>

<template>
  <article
    class="h-full overflow-hidden rounded-lg border border-neutral-border bg-neutral-surface transition-shadow hover:shadow-md"
  >
    <RouterLink
      :to="{ name: 'news-detail', params: { id: news.id } }"
      class="flex h-full flex-col focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
    >
      <!-- 封面：預設直式 4:5（規格第二十節），後台上傳 1200 × 1500 的圖不會被裁切 -->
      <!-- 首頁輪播改用 1:1 圖框，海報完整縮放、四周留底色 -->
      <img
        :src="news.imagePath"
        :alt="news.title"
        loading="lazy"
        class="w-full"
        :class="coverClass"
      />

      <div class="flex flex-1 flex-col gap-2 p-4">
        <div class="flex flex-wrap items-center gap-2">
          <UBadge v-if="news.isPinned" color="action" variant="solid">置頂</UBadge>
          <!-- 分類統一 primary 色，用文字區分（前台規則不新增顏色） -->
          <UBadge color="primary" variant="subtle">{{ news.categoryName }}</UBadge>
          <time :datetime="news.publishedAt" class="text-sm text-neutral-text-secondary">
            {{ publishedDate }}
          </time>
        </div>

        <!-- 標題最多兩行、摘要最多三行，超過顯示 …，卡片高度才會一致 -->
        <h3 class="line-clamp-2 text-base font-semibold text-neutral-text-primary">
          {{ news.title }}
        </h3>
        <p class="line-clamp-3 text-sm text-neutral-text-secondary">
          {{ news.summary }}
        </p>
      </div>
    </RouterLink>
  </article>
</template>
