<!--
    MyReviewView.vue — 我的評論（取代 Views/CReview/ShowMyReviewPage.cshtml）

    ── 10/10：內容抽到 components/review/MyReviewDetail.vue ─────────────
    會員中心「我的評論 › 已評論」展開後也要顯示同樣的內容，所以把評論卡片、館方回覆、表態、
    公開切換、讀取與記錄已讀的邏輯整段搬到 MyReviewDetail（程式碼沒改）。
    這個檔案只剩外框：標題、預覽提示、返回按鈕。下面的說明仍然適用，只是程式碼在 MyReviewDetail 裡。
    返回按鈕：顧客改回「我的評論」（會員中心現在是唯一的入口）；員工預覽維持「返回評論專區」。

    路由：/reviews/visit/:id           → kind='visit'（id ＝ 現場評論憑證 ReviewPerVisitId）
          /reviews/booking/:orderId    → kind='booking'
          /reviews/preview/:reviewId   → previewId（員工預覽，從館方清單開過來）

    ── 員工預覽（2026-09-26 取代原本的 ?preview=1）──────────────
    跟顧客用同一個畫面，員工看到的就是顧客看到的（包括「館方回覆了你的評論」提示）。
    差別只有三個：
      1. 資料從員工專用的 API 拿（規格 2-8）。顧客的評論要會員本人登入才打得開，
         員工用這支才看得到。
      2. 所有會改資料的按鈕都停用（公開開關、滿意度）。
      3. 不送「已看過回覆」的紀錄，不會汙染營運分析的已讀率。
    原本的 ?preview=1 拿掉了：它只是一個網址參數，有沒有加全靠自覺。

    ── 「顧客已看過回覆」改成另外一支 POST ─────────────────────
    Razor 版是「打開頁面（GET）就順便寫入 ReplyViewedAt」。改成兩步：
      1. GET 只讀資料
      2. 頁面顯示出來之後，若有回覆且還沒記錄過，才 POST /reply-viewed
    好處有兩個：
      ・員工預覽時不送第 2 步，就不會汙染營運分析的已讀率（交接文件第三節第 8 項）
      ・「館方回覆了你的評論」提示終於看得到了。
        Razor 版是先寫入 ReplyViewedAt 才組 ViewModel，
        所以 HasUnviewedReply 永遠是 false，那個提示從來沒出現過。
        現在 GET 拿到的是寫入之前的狀態，第一次打開時提示會正常出現。
-->
<script setup>
import { computed } from "vue";
import MyReviewDetail from "@/components/review/MyReviewDetail.vue";

// 顧客模式傳 kind + ticket；預覽模式只傳 previewId（種類由 API 回傳的 kind 決定）
const props = defineProps({
  kind: { type: String, default: null },
  ticket: { type: String, default: null },
  previewId: { type: String, default: null },
});

const isPreview = computed(() => props.previewId != null);
</script>

<template>
  <main class="mx-auto max-w-2xl px-4 py-10 md:px-6">
    <h1 class="mb-6 text-2xl font-bold text-neutral-text-primary">我的評論</h1>

    <UAlert
      v-if="isPreview"
      color="neutral"
      variant="outline"
      icon="i-lucide-eye"
      title="預覽模式：這是顧客看到的畫面"
      description="按鈕都已停用，不會改到任何資料，也不會記錄「顧客已看過回覆」。"
      class="mb-4"
    />

    <MyReviewDetail :kind="kind" :ticket="ticket" :preview-id="previewId" />

    <UButton
      v-if="isPreview"
      :to="{ name: 'review-index' }"
      color="primary"
      variant="outline"
      icon="i-lucide-arrow-left"
      label="返回評論專區"
      class="mt-6"
    />
    <UButton
      v-else
      :to="{ name: 'member-reviews', query: { tab: 'reviewed' } }"
      color="primary"
      variant="outline"
      icon="i-lucide-arrow-left"
      label="回我的評論"
      class="mt-6"
    />
  </main>
</template>
