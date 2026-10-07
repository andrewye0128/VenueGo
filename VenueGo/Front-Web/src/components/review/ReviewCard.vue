<!--
  ReviewCard.vue — 評論專區的一張卡片

  ⚠️ 顧客端不顯示員工姓名與顧客滿意度。
     API 回傳的卡片資料「結構上就沒有」那些欄位（規格 2-1），
     防洩漏靠的是資料形狀，不是靠這裡記得不要寫。
-->
<script setup>
import { ref, onMounted, nextTick } from "vue";
import StarView from "./StarView.vue";
import TimeText from "@/components/TimeText.vue";

defineProps({
  item: { type: Object, required: true },
});

// ── 內容截斷 ──
// 收合時固定四行（line-clamp-4）。只有內容真的超過四行，才顯示「展開全文」。
// scrollHeight（完整內容多高）比 clientHeight（現在看得到多高）大，就代表被截掉了。留 2px 給四捨五入的誤差。
const bodyEl = ref(null);
const expanded = ref(false);
const clamped = ref(false);

onMounted(async () => {
  await nextTick();
  const el = bodyEl.value;
  if (el) clamped.value = el.scrollHeight > el.clientHeight + 2;
});
</script>

<template>
  <!-- 只給星等的評論用虛線框、淺底，一眼分得出「沒有文字」 -->
  <article
    class="rounded-lg border p-4"
    :class="
      item.isRatingOnly
        ? 'border-dashed border-neutral-border bg-brand-background'
        : 'border-neutral-border bg-neutral-surface'
    "
  >
    <!-- 頁首：星等、署名、時間 -->
    <div class="mb-2 flex items-start justify-between gap-2">
      <div class="flex flex-wrap items-center gap-2">
        <StarView :rating="item.starRating" />
        <span class="font-semibold text-neutral-text-primary">{{ item.displayName }}</span>
      </div>
      <TimeText :time="item.createdAt" class="shrink-0 text-sm text-neutral-text-secondary" />
    </div>

    <!-- 內容 -->
    <p v-if="item.isRatingOnly" class="mb-2 text-sm text-neutral-text-secondary italic">
      這位使用者只留下評分，沒有寫評論。
    </p>
    <template v-else>
      <!-- v-text 跟 {{ }} 一樣會把內容當成純文字，顧客寫什麼都不會被當成 HTML。
           用 v-text 而不是 {{ }}：排版工具會把 {{ }} 換到下一行，多出來的空白在 whitespace-pre-wrap 底下會顯示出來。
           ⚠️ 不要改成 v-html：評論是顧客輸入的，那等於開 XSS 的門。
           whitespace-pre-wrap 保留顧客的換行；wrap-anywhere 讓很長的英數字串也能折行，不撐破卡片。 -->
      <p
        ref="bodyEl"
        class="mb-1 whitespace-pre-wrap wrap-anywhere text-neutral-text-primary"
        :class="expanded ? '' : 'line-clamp-4'"
        v-text="item.content"
      ></p>
      <UButton
        v-if="clamped"
        color="primary"
        variant="ghost"
        size="sm"
        :label="expanded ? '收合' : '展開全文'"
        :aria-expanded="expanded"
        class="-ms-3 mb-1"
        @click="expanded = !expanded"
      />
    </template>

    <!-- 提及標籤 -->
    <div v-if="item.mentionsVenue || item.mentionsStaff" class="mb-2 flex flex-wrap gap-1">
      <UBadge
        v-if="item.mentionsVenue"
        color="neutral"
        variant="outline"
        icon="i-lucide-hash"
        label="場地設施"
      />
      <UBadge
        v-if="item.mentionsStaff"
        color="neutral"
        variant="outline"
        icon="i-lucide-hash"
        label="人員服務"
      />
    </div>

    <!-- 館方回覆：只有內容與時間 -->
    <div
      v-if="item.reply"
      class="mt-2 rounded-lg border-l-4 border-l-brand-primary bg-brand-background p-3"
    >
      <div class="mb-1 flex items-center gap-1 text-sm text-neutral-text-secondary">
        <UIcon name="i-lucide-reply" aria-hidden="true" />
        館方回覆
        <TimeText :time="item.reply.repliedAt" class="ms-2" />
      </div>
      <p
        class="whitespace-pre-wrap wrap-anywhere text-neutral-text-primary"
        v-text="item.reply.content"
      ></p>
    </div>

    <!-- 頁尾：場地 -->
    <div
      v-if="item.venueName"
      class="mt-2 flex items-center gap-1 text-sm text-neutral-text-secondary"
    >
      <UIcon name="i-lucide-map-pin" aria-hidden="true" />
      {{ item.venueName }}
    </div>
  </article>
</template>
