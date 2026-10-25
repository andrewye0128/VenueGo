<!--
  MemberReviewsMarker.vue — 會員中心選單「我的評論」右邊的提醒（昱，10/25）
  用法（layouts/member/MemberLayout.vue，選單的 RouterLink 裡、文字後面）：
    <MemberReviewsMarker v-if="item.to === '/member/reviews'" />

  ── 顯示什麼 ─────────────────────────────────────────────
  - 有快到期的待評論：橘色數字（快到期的則數）。
  - 沒有快到期、但有館方新回覆（顧客還沒看過）：藍色的「新」。
  - 兩個都有時只顯示橘色：快到期關係到顧客能不能評論，比新回覆重要。
  - 都沒有：不顯示。

  ── 什麼時候顯示、什麼時候消失 ──────────────────────────
  1. 每次「進入會員中心」讀一次（從網站其他地方進來、重新整理）。
     會員中心裡面換頁（個人資料 → 我的預約）不重讀：這個元件跟著會員中心的版面，換頁時不會重建。
  2. 一打開「我的評論」就消失，之後在會員中心裡換到其他頁也不再出現，直到下一次進入會員中心。
     剛剛在我的評論頁看過了，短時間內再提醒一次是多餘的；而且我的評論頁的分頁上也有同樣的提醒，畫面上不要重複。
  3. 直接進到我的評論（例如從導覽列「我的評論」、或在我的評論頁重新整理）：不讀也不顯示。

  讀不到（沒登入、後端出錯）就不顯示，不跳錯誤：提醒只是附加的，不能讓會員中心跟著壞掉。
  讀的是「我的評論」頁同樣的兩支 API（待評論、已評論），規則跟那一頁分頁上的提醒一致。
-->
<script setup>
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { listPendingReviews, listReviewedReviews } from "@/api/reviewApi";

const REVIEWS_ROUTE = "member-reviews";

const route = useRoute();
const expiring = ref(0); // 快到期的待評論則數
const newReplies = ref(0); // 有館方新回覆的則數
const dismissed = ref(false); // 這次進入會員中心之後，打開過我的評論了

async function load() {
  // 兩份各自讀：一份失敗，另一份照樣顯示
  const [pending, reviewed] = await Promise.allSettled([
    listPendingReviews(),
    listReviewedReviews(),
  ]);
  if (pending.status === "fulfilled" && Array.isArray(pending.value)) {
    expiring.value = pending.value.filter((item) => item.deadline?.expiringSoon).length;
  }
  if (reviewed.status === "fulfilled" && Array.isArray(reviewed.value)) {
    newReplies.value = reviewed.value.filter((item) => item.hasUnviewedReply).length;
  }
}

// 檔頭第 1、3 點：一進來就在我的評論 → 不讀；否則讀一次
if (route.name === REVIEWS_ROUTE) dismissed.value = true;
else load();

// 檔頭第 2 點：打開我的評論就消失，之後不再出現（直到下一次進入會員中心，這個元件重建）
watch(
  () => route.name,
  (name) => {
    if (name === REVIEWS_ROUTE) dismissed.value = true;
  },
);

const show = computed(() => !dismissed.value && (expiring.value > 0 || newReplies.value > 0));
</script>

<template>
  <!-- ml-auto：靠右；右邊的空間是選單項目本身的 padding，不會貼齊邊框 -->
  <span v-if="show" class="ml-auto inline-flex shrink-0">
    <template v-if="expiring">
      <UBadge
        :label="expiring"
        color="action"
        variant="solid"
        size="sm"
        :title="`${expiring} 則待評論快到期`"
      />
      <span class="sr-only">，{{ expiring }} 則待評論快到期</span>
    </template>
    <template v-else>
      <UBadge
        label="新"
        color="primary"
        variant="solid"
        size="sm"
        :title="`${newReplies} 則有館方新回覆`"
      />
      <span class="sr-only">，{{ newReplies }} 則有館方新回覆</span>
    </template>
  </span>
</template>
