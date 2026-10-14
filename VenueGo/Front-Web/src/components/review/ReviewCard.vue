<!--
  ReviewCard.vue — 公開評論卡片：評論專區、首頁「顧客評價」Slide 共用

  ⚠️ 顧客端不顯示員工姓名與顧客滿意度。
     API 回傳的卡片資料「結構上就沒有」那些欄位（規格 2-1），
     防洩漏靠的是資料形狀，不是靠這裡記得不要寫。

  ── 10/13 改版（參考 Airbnb 的評論卡、Reddit／TikTok 的留言串）──────────────
  1. 左上角圓形頭像（名字的第一個字），顏色依名字固定分配（utils/review/avatarTone.js）：
     一眼分得出是不同的人，整頁也不再是一片白。
  2. 名字旁邊放時間；第二行是星等＋場地。原本場地在卡片最底下，讀完全文才知道是哪個場地。
  3. 館方回覆改成留言串：左邊一條細線＋「館」的小頭像，跟「我的評論」的回覆泡泡同一個語言。
  4. 只給星等的評論不再用虛線框＋灰底（看起來像「壞掉的卡片」），改成一行淡色小字。

  ── 10/14 改版：卡片等高、全文改用視窗看 ──────────────────────────────
  5. 卡片一律撐滿外層給的高度（h-full），評論專區兩欄時同一列等高，卡片之間的空隙一律相同。
     原本的「原地展開全文」會把同一列另一張也撐高，所以拿掉了。
  6. 評論固定截斷 4 行、館方回覆 2 行；兩者任一真的被截掉，才顯示「閱讀全文」，
     按了開中央視窗看完整內容（點視窗外任何地方、按 Esc 或右上角 × 都會關閉）。
     首頁 Slide 和評論專區是同一套，不再分 compact。
  7. 館方回覆緊接在評論下面，等高多出來的空白留在卡片底部。
     （試過把回覆固定在卡片底部：首頁 Slide 的卡片高度跟最長的那張一樣，回覆會跟評論隔一大段，看起來像兩件事。）
  8. full：視窗裡用的完整版，不截斷、沒有外框、沒有「閱讀全文」。
-->
<script setup>
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from "vue";
import StarView from "./StarView.vue";
import TimeText from "@/components/TimeText.vue";
import { avatarTone, avatarInitial } from "@/utils/review/avatarTone";

const props = defineProps({
  item: { type: Object, required: true },
  // 10/14：視窗裡的完整版（見檔頭第 8 點）
  full: { type: Boolean, default: false },
});

const tone = computed(() => avatarTone(props.item.displayName));
const initial = computed(() => avatarInitial(props.item.displayName));

// ── 有沒有被截斷 ──
// scrollHeight（完整內容多高）比 clientHeight（現在看得到多高）大，就代表被截掉了。留 2px 給四捨五入的誤差。
// 卡片寬度會變（視窗縮放、Slide 換斷點），截斷與否跟著變，所以用 ResizeObserver 重新量。
const bodyEl = ref(null);
const replyEl = ref(null);
const clamped = ref(false);
const fullOpen = ref(false);
let observer = null;

function isCut(el) {
  return !!el && el.scrollHeight > el.clientHeight + 2;
}
function measure() {
  clamped.value = isCut(bodyEl.value) || isCut(replyEl.value);
}

onMounted(async () => {
  if (props.full) return; // 完整版不截斷，不用量
  await nextTick();
  measure();
  if (typeof ResizeObserver !== "undefined") {
    observer = new ResizeObserver(measure);
    [bodyEl.value, replyEl.value].forEach((el) => el && observer.observe(el));
  }
});
onBeforeUnmount(() => observer?.disconnect());
</script>

<template>
  <article
    class="flex flex-col"
    :class="
      full ? '' : 'h-full rounded-xl border border-neutral-border bg-neutral-surface p-4 md:p-5'
    "
  >
    <!-- 頁首：頭像、名字、時間；第二行星等與場地 -->
    <header class="flex items-start gap-3">
      <span
        class="flex size-10 shrink-0 items-center justify-center rounded-full text-base font-bold"
        :class="tone"
        aria-hidden="true"
      >
        {{ initial }}
      </span>
      <div class="min-w-0 flex-1">
        <div class="flex items-baseline justify-between gap-2">
          <span class="truncate font-semibold text-neutral-text-primary">{{
            item.displayName
          }}</span>
          <TimeText :time="item.createdAt" class="shrink-0 text-xs text-neutral-text-secondary" />
        </div>
        <div class="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-1">
          <StarView :rating="item.starRating" size-class="text-sm" />
          <span
            v-if="item.venueName"
            class="inline-flex min-w-0 items-center gap-1 text-sm text-neutral-text-secondary"
          >
            <UIcon name="i-lucide-map-pin" class="shrink-0" aria-hidden="true" />
            <span class="truncate">{{ item.venueName }}</span>
          </span>
        </div>
      </div>
    </header>

    <!-- 內容 -->
    <div class="mt-3">
      <p v-if="item.isRatingOnly" class="text-sm text-neutral-text-secondary italic">
        只留下了評分
      </p>
      <template v-else>
        <!-- v-text 跟 {{ }} 一樣會把內容當成純文字，顧客寫什麼都不會被當成 HTML。
             用 v-text 而不是 {{ }}：排版工具會把 {{ }} 換到下一行，多出來的空白在 whitespace-pre-wrap 底下會顯示出來。
             ⚠️ 不要改成 v-html：評論是顧客輸入的，那等於開 XSS 的門。
             whitespace-pre-wrap 保留顧客的換行；wrap-anywhere 讓很長的英數字串也能折行，不撐破卡片。 -->
        <p
          ref="bodyEl"
          class="whitespace-pre-wrap wrap-anywhere text-neutral-text-primary"
          :class="full ? '' : 'line-clamp-4'"
          v-text="item.content"
        ></p>
      </template>

      <!-- 提及標籤 -->
      <div v-if="item.mentionsVenue || item.mentionsStaff" class="mt-2 flex flex-wrap gap-1">
        <UBadge
          v-if="item.mentionsVenue"
          color="neutral"
          variant="soft"
          size="sm"
          icon="i-lucide-hash"
          label="場地設施"
        />
        <UBadge
          v-if="item.mentionsStaff"
          color="neutral"
          variant="soft"
          size="sm"
          icon="i-lucide-hash"
          label="人員服務"
        />
      </div>

      <!-- 評論或回覆被截掉才出現（檔頭第 6 點） -->
      <UButton
        v-if="clamped"
        color="primary"
        variant="link"
        size="sm"
        label="閱讀全文"
        trailing-icon="i-lucide-chevron-right"
        aria-haspopup="dialog"
        class="mt-1 -ms-2.5"
        @click="fullOpen = true"
      />
    </div>

    <!-- 館方回覆：留言串樣式（左邊細線＋小頭像），只有內容與時間 -->
    <div v-if="item.reply" class="mt-3 flex gap-2.5 border-l-2 border-brand-primary/30 pl-3">
      <span
        class="flex size-6 shrink-0 items-center justify-center rounded-full bg-brand-primary text-xs font-bold text-white"
        aria-hidden="true"
      >
        館
      </span>
      <div class="min-w-0 flex-1">
        <div class="flex flex-wrap items-baseline gap-x-2 text-sm">
          <span class="font-semibold text-neutral-text-primary">館方回覆</span>
          <TimeText :time="item.reply.repliedAt" class="text-xs text-neutral-text-secondary" />
        </div>
        <p
          ref="replyEl"
          class="mt-0.5 text-sm whitespace-pre-wrap wrap-anywhere text-neutral-text-primary"
          :class="full ? '' : 'line-clamp-2'"
          v-text="item.reply.content"
        ></p>
      </div>
    </div>

    <!-- 完整內容視窗。Nuxt UI 的 UModal 預設：點視窗外任何地方、按 Esc、按右上角 × 都會關閉 -->
    <UModal
      v-if="!full"
      v-model:open="fullOpen"
      title="評價全文"
      description="完整的評論內容與館方回覆"
      :ui="{ description: 'sr-only', content: 'max-w-xl' }"
    >
      <template #body>
        <ReviewCard :item="item" full />
      </template>
    </UModal>
  </article>
</template>
