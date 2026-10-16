<script setup>
// 常見問題的回答：把後端切好的片段畫成文字與連結
// 連結解析在後端 FaqAnswerParser，這裡只負責依片段類型顯示，不自己解析、不用 v-html
// UIcon 由 Nuxt UI 自動匯入
defineProps({
  // 資料格式同 GET /api/faqs 的 answerSegments：
  // [{ type: "text" | "link", text, url, isExternal }]
  segments: { type: Array, required: true },
});

// 連結共用的樣式
const linkClass =
  "text-brand-primary underline underline-offset-2 hover:text-brand-action focus-visible:outline-2 focus-visible:outline-brand-primary";
</script>

<template>
  <!-- whitespace-pre-line：保留後台輸入的換行 -->
  <!-- 片段之間不能有空白或換行，否則 pre-line 會把它們顯示成多出來的空格，所以寫在同一行 -->
  <p class="leading-relaxed whitespace-pre-line">
    <template v-for="(segment, index) in segments" :key="index"
      ><RouterLink
        v-if="segment.type === 'link' && !segment.isExternal"
        :to="segment.url"
        :class="linkClass"
        >{{ segment.text }}</RouterLink
      ><a
        v-else-if="segment.type === 'link'"
        :href="segment.url"
        target="_blank"
        rel="noopener noreferrer"
        :class="linkClass"
        >{{ segment.text
        }}<UIcon name="i-mdi-open-in-new" class="ms-0.5 inline size-3.5 align-middle" /><span
          class="sr-only"
          >（另開新分頁）</span
        ></a
      ><template v-else>{{ segment.text }}</template></template
    >
  </p>
</template>
