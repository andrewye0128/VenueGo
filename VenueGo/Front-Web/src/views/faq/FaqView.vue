<script setup>
// 常見問題頁 /faq：分類頁籤（沒有「全部」）+ 手風琴式問答
// 進頁面呼叫一次 API 拿到全部分類，切換頁籤只在前端切換、不重新呼叫
// 目前的分類放在網址上（/faq?category=venue）：重新整理、分享網址都會停在同一個分類
// UAccordion、UButton、USkeleton 由 Nuxt UI 自動匯入
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { getFaqs } from "@/api/faqApi";
import FaqAnswer from "@/components/faq/FaqAnswer.vue";

const route = useRoute();
const router = useRouter();

// [{ category, categoryName, faqs: [{ id, question, answerSegments }] }]，分類順序由後端決定
const groups = ref([]);
const isLoading = ref(true);
const errorMessage = ref("");

onMounted(async () => {
  try {
    groups.value = await getFaqs();
  } catch (error) {
    // http.js 已經把錯誤轉成中文訊息
    errorMessage.value = error.message;
  } finally {
    isLoading.value = false;
  }
});

// 目前的分類：網址上的分類存在就用它；沒有帶或被亂改時，用第一個分類（預約與付款）
const currentGroup = computed(
  () => groups.value.find((group) => group.category === route.query.category) ?? groups.value[0],
);

// 手風琴的資料：label 是問題（顯示在標題列），segments 放進 #body 插槽畫出回答
const accordionItems = computed(() =>
  (currentGroup.value?.faqs ?? []).map((faq) => ({
    label: faq.question,
    value: String(faq.id),
    segments: faq.answerSegments,
  })),
);

// 切換分類：只改網址，資料已經在手上，不重新呼叫 API
function selectCategory(category) {
  router.push({ query: { category } });
}

// 手風琴外觀：加上外框與底色，只用 Tailwind 標準 class 與色票
const accordionUi = {
  root: "rounded-lg border border-neutral-border bg-neutral-surface px-4",
  trigger: "text-base text-neutral-text-primary",
  body: "text-base text-neutral-text-secondary",
};

// 「找不到答案？」的聯絡電話：與頁尾 AppFooter.vue 的電話同步，修改時兩邊都要改
const CONTACT_PHONE = "(02) 1234-5678";
</script>

<template>
  <main class="mx-auto max-w-4xl px-4 py-10 md:px-6">
    <h1 class="text-2xl font-bold text-neutral-text-primary">常見問題</h1>

    <!-- 載入中：頁籤與問題列的骨架 -->
    <template v-if="isLoading">
      <div class="mt-6 flex flex-wrap gap-2">
        <USkeleton v-for="n in 5" :key="n" class="h-8 w-24 rounded-md" />
      </div>
      <div class="mt-6 flex flex-col gap-3">
        <USkeleton v-for="n in 4" :key="n" class="h-12 w-full rounded-lg" />
      </div>
    </template>

    <p
      v-else-if="errorMessage"
      class="mt-6 rounded-lg border border-semantic-error bg-neutral-surface p-4 text-sm text-semantic-error"
    >
      常見問題暫時無法載入：{{ errorMessage }}
    </p>

    <template v-else>
      <!-- 分類頁籤：選中的實心、其他外框（跟最新消息列表一致）；沒有「全部」 -->
      <div class="mt-6 flex flex-wrap gap-2">
        <UButton
          v-for="group in groups"
          :key="group.category"
          color="primary"
          size="sm"
          :variant="currentGroup?.category === group.category ? 'solid' : 'outline'"
          :aria-pressed="currentGroup?.category === group.category"
          @click="selectCategory(group.category)"
        >
          {{ group.categoryName }}
        </UButton>
      </div>

      <p v-if="accordionItems.length === 0" class="mt-6 text-sm text-neutral-text-secondary">
        目前沒有這個分類的問題
      </p>

      <!-- type="multiple"：可以同時展開多題；進頁面全部收起 -->
      <!-- :key：切換分類時重建手風琴，已展開的題目全部收起 -->
      <UAccordion
        v-else
        :key="currentGroup.category"
        type="multiple"
        :items="accordionItems"
        :ui="accordionUi"
        class="mt-6"
      >
        <template #body="{ item }">
          <FaqAnswer :segments="item.segments" />
        </template>
      </UAccordion>
    </template>

    <!-- 找不到答案時的下一步 -->
    <section
      class="mt-10 rounded-lg border border-neutral-border bg-neutral-surface p-6 text-center"
    >
      <h2 class="text-lg font-bold text-neutral-text-primary">找不到您要的答案嗎？</h2>
      <p class="mt-2 text-neutral-text-secondary">
        歡迎來電
        <a
          :href="`tel:${CONTACT_PHONE.replace(/[^0-9]/g, '')}`"
          class="font-semibold text-brand-primary underline underline-offset-2 hover:text-brand-action"
          >{{ CONTACT_PHONE }}</a
        >，服務人員會協助您。
      </p>
    </section>
  </main>
</template>
