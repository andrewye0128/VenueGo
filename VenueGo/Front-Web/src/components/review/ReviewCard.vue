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
  9. 10/14 晚：「閱讀全文 >」靠右。從左讀到右，讀到這一段的結尾才看到「還有下文」，箭頭也指向右邊。
  10. 10/15：右上角改成「✓ 10/12 到館」（第一次有效入場的日期），下面一行小字「10/14 評價」；
      同一天就只寫到館。只有日期、不寫幾點：公開掛名「某人幾點在場館」算個人行蹤，精確時間只在「我的評論」顯示。
  11. 10/15：「館方回覆」四個字從黑色改成品牌藍，跟小頭像同一個藍，一眼就認得出是館方說的話。
      左邊引導線維持 2px 細線，顏色從 30% 淡藍調到 60%（實色太搶眼，30% 又太淡，取中間）。
      （試過加淡藍底、3px 實色線，都覺得不好看，拿掉了。）
  12. 10/14 晚：提及標籤兩種顏色（藍＝場地設施、橘＝人員服務），圖示也不同（constants/review.js 的 MENTION_TAGS）。

  ── 10/16：首頁輪播的短評留白（featured，只有 HomeReviewCarousel 會傳）──────────
  13. 卡片等高，短評的卡片下方會空一大塊。只在首頁輪播處理，評論專區維持現狀：
      - 短評（SHORT_TEXT_MAX 字以內）字級放大一級（text-lg），一句話不會縮在左上角。
        首頁只放 3 星以上，放大不會把負評放大；評論專區什麼星等都有，短的負評放大看起來會像在吼，所以不做。
      - 留白夠高時，右下角放一個淡色的大引號 ”：首頁是網站在「引用」顧客的話，引號的意思是對的。
        （館方回覆區不放引號：那裡是館方自己在說話。）
        留白不夠高就不顯示（ResizeObserver 量留白的高度），不會壓到文字，也不會把最高的那張卡片撐得更高。
        只給星等的評論不放引號：沒有話可以引用（10/16 起首頁也可能出現只給星等的評論）。

  ── 10/16：提及標籤和「閱讀全文」放同一行 ───────────────────────────
  14. 只有標籤、只有按鈕、兩個都有，這一行都一樣高（20px）；兩個都沒有才整行不出現。
      Nuxt UI（4.11.2）的 sm 標籤是 20px 高（text-[10px]/3＋py-1），sm 按鈕是 28px（text-xs 16px＋py-1.5）。
      按鈕上下各用 -my-1 收回 4px：在版面上只佔 20px，跟標籤一樣高；能點的範圍還是 28px，手指一樣好按。
      標籤在左、按鈕靠右（第 9 點）。手機上兩個標籤加按鈕放不下時，標籤會自己折成兩行，這時這一行會變高。

  ── 10/16 晚：頁首重排、內距、評論專區字級 ─────────────────────────────
  15. 頁首分成兩邊，各講一件事：
      - 左邊是「誰、怎麼評、哪天評」：名字；下一行星等，右邊緊接評論日期（只有日期，不寫「評價」兩個字）。
      - 右邊是「去了哪裡、哪天去」，都是入場紀錄證明過的事實：第一行場地，第二行「✓ 10/12 到館」。
      - 場地用品牌藍：這張卡片裡藍色本來就代表「場館」（「場地設施」標籤、「館」小頭像、「館方回覆」都是藍）。
        到館改成綠色（semantic-success）：綠色在全站的意思是「確認過、沒問題」，跟「依入場紀錄」的勾勾意思一致。
      - 評論日期和到館日期同一天也兩個都寫：兩個日期在不同位置、講不同的事，不再合併成一行。
  16. 左右內距 24／32px：內容不會被首頁輪播的左右箭頭壓到，看起來也比較不擠。上維持 16／20px，下 20／28px。
  17. 評論專區（沒有傳 featured）的文字都大 1px：內文 17px、名字 17px、回覆 15px、日期與場地 13～15px。
      所有評論一起放大，不會只把某一則（例如很短的負評）凸顯出來；首頁輪播維持原本的字級（第 13 點的短評放大另外算）。
      標籤和「閱讀全文」不跟著放大：它們那一行固定 20px 高（第 14 點）。
-->
<script setup>
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from "vue";
import StarView from "./StarView.vue";
import TimeText from "@/components/TimeText.vue";
import { avatarTone, avatarInitial } from "@/utils/review/avatarTone";
import { MENTION_TAGS } from "@/constants/review";
import { shortDate } from "@/utils/review/shortDate";

const props = defineProps({
  item: { type: Object, required: true },
  // 10/14：視窗裡的完整版（見檔頭第 8 點）
  full: { type: Boolean, default: false },
  // 10/16：首頁輪播用（見檔頭第 13 點）
  featured: { type: Boolean, default: false },
});

// ── 首頁輪播的短評（10/16，檔頭第 13 點）──
const SHORT_TEXT_MAX = 40; // 字數以內算短評
const QUOTE_MIN_SPACE_PX = 48; // 留白至少這麼高才放引號
// 用 [...文字] 算字數：中文、emoji 都算 1 個字（.length 會把部分 emoji 算成 2）
const isShort = computed(
  () =>
    props.featured &&
    !props.full &&
    !props.item.isRatingOnly &&
    [...(props.item.content ?? "")].length <= SHORT_TEXT_MAX,
);

// ── 到館日期（10/15，檔頭第 10 點）──
// 只有日期，不寫幾點（第 10 點）。10/16 晚：評論日期移到星等右邊，跟到館同一天也照寫（第 15 點）
const visitedText = computed(() => (props.item.visitedOn ? shortDate(props.item.visitedOn) : ""));
const reviewedText = computed(() =>
  props.item.createdAt?.value ? shortDate(props.item.createdAt.value) : "",
);

// ── 字級（10/16 晚，檔頭第 17 點）：評論專區大 1px，首頁輪播維持原樣 ──
const sz = computed(() =>
  props.featured
    ? { name: "", body: "", reply: "text-sm", meta: "text-xs", venue: "text-sm" }
    : {
        name: "text-[17px]",
        body: "text-[17px]",
        reply: "text-[15px]",
        meta: "text-[13px]",
        venue: "text-[15px]",
      },
);

const tone = computed(() => avatarTone(props.item.displayName));
const initial = computed(() => avatarInitial(props.item.displayName));

// ── 有沒有被截斷 ──
// scrollHeight（完整內容多高）比 clientHeight（現在看得到多高）大，就代表被截掉了。留 2px 給四捨五入的誤差。
// 卡片寬度會變（視窗縮放、Slide 換斷點），截斷與否跟著變，所以用 ResizeObserver 重新量。
const bodyEl = ref(null);
const replyEl = ref(null);
const spacerEl = ref(null); // 卡片底部的留白（只有首頁輪播有）
const clamped = ref(false);
const quoteFits = ref(false);
const fullOpen = ref(false);
let observer = null;

function isCut(el) {
  return !!el && el.scrollHeight > el.clientHeight + 2;
}
function measure() {
  clamped.value = isCut(bodyEl.value) || isCut(replyEl.value);
  quoteFits.value = !!spacerEl.value && spacerEl.value.clientHeight >= QUOTE_MIN_SPACE_PX;
}

onMounted(async () => {
  if (props.full) return; // 完整版不截斷，不用量
  await nextTick();
  measure();
  if (typeof ResizeObserver !== "undefined") {
    observer = new ResizeObserver(measure);
    [bodyEl.value, replyEl.value, spacerEl.value].forEach((el) => el && observer.observe(el));
  }
});
onBeforeUnmount(() => observer?.disconnect());
</script>

<template>
  <article
    class="flex flex-col"
    :class="
      full
        ? ''
        : 'h-full rounded-xl border border-neutral-border bg-neutral-surface px-6 md:px-8 pt-4 pb-5 md:pt-5 md:pb-7'
    "
  >
    <!-- 頁首：左邊名字、星等＋評論日期；右邊場地＋到館日期（檔頭第 15 點） -->
    <header class="flex items-start gap-3">
      <span
        class="flex size-10 shrink-0 items-center justify-center rounded-full text-base font-bold"
        :class="tone"
        aria-hidden="true"
      >
        {{ initial }}
      </span>
      <!-- 左右兩欄：右欄有兩行時只撐高右欄，不會把星等那一行往下推 -->
      <div class="flex min-w-0 flex-1 items-start justify-between gap-2">
        <div class="min-w-0">
          <span
            class="block truncate font-semibold text-neutral-text-primary"
            :class="sz.name"
            v-text="item.displayName"
          ></span>
          <div class="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-1">
            <StarView :rating="item.starRating" size-class="text-sm" />
            <span
              v-if="reviewedText"
              class="text-neutral-text-secondary tabular-nums"
              :class="sz.meta"
              v-text="reviewedText"
            ></span>
          </div>
        </div>
        <div
          v-if="item.venueName || visitedText"
          class="flex shrink-0 flex-col items-end gap-1 pt-0.5 text-right leading-tight"
        >
          <span
            v-if="item.venueName"
            class="inline-flex items-center gap-1 font-semibold text-brand-primary"
            :class="sz.venue"
          >
            <UIcon name="i-lucide-map-pin" class="size-3.5 shrink-0" aria-hidden="true" />
            {{ item.venueName }}
          </span>
          <span
            v-if="visitedText"
            class="inline-flex items-center gap-1 font-semibold text-semantic-success tabular-nums"
            :class="sz.meta"
            title="依入場紀錄"
          >
            <UIcon name="i-lucide-badge-check" class="size-3.5 shrink-0" aria-hidden="true" />
            {{ visitedText }} 到館
          </span>
        </div>
      </div>
    </header>

    <!-- 內容 -->
    <div class="mt-3">
      <p v-if="item.isRatingOnly" class="text-neutral-text-secondary italic" :class="sz.reply">
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
          :class="[full ? '' : 'line-clamp-4', isShort ? 'text-lg' : sz.body]"
          v-text="item.content"
        ></p>
      </template>

      <!-- 提及標籤＋「閱讀全文」同一行（檔頭第 14 點）。「閱讀全文」只在評論或回覆被截掉時出現（第 6 點） -->
      <div
        v-if="item.mentionsVenue || item.mentionsStaff || clamped"
        class="mt-2 flex min-h-5 items-center gap-2"
      >
        <div class="flex min-w-0 flex-wrap gap-1">
          <UBadge
            v-if="item.mentionsVenue"
            :color="MENTION_TAGS.venue.color"
            variant="soft"
            size="sm"
            :icon="MENTION_TAGS.venue.icon"
            :label="MENTION_TAGS.venue.label"
          />
          <UBadge
            v-if="item.mentionsStaff"
            :color="MENTION_TAGS.staff.color"
            variant="soft"
            size="sm"
            :icon="MENTION_TAGS.staff.icon"
            :label="MENTION_TAGS.staff.label"
          />
        </div>
        <UButton
          v-if="clamped"
          color="primary"
          variant="link"
          size="sm"
          label="閱讀全文"
          trailing-icon="i-lucide-chevron-right"
          aria-haspopup="dialog"
          class="-my-1 ms-auto -me-2.5 shrink-0"
          @click="fullOpen = true"
        />
      </div>
    </div>

    <!-- 館方回覆：留言串樣式（左邊細線＋小頭像），只有內容與時間 -->
    <!-- 10/15：引導線維持 2px，顏色 30% → 60%（檔頭第 11 點） -->
    <div v-if="item.reply" class="mt-3 flex gap-2.5 border-l-2 border-brand-primary/60 pl-3">
      <span
        class="flex size-6 shrink-0 items-center justify-center rounded-full bg-brand-primary text-xs font-bold text-white"
        aria-hidden="true"
      >
        館
      </span>
      <div class="min-w-0 flex-1">
        <div class="flex flex-wrap items-baseline gap-x-2" :class="sz.reply">
          <span class="font-semibold text-brand-primary">館方回覆</span>
          <TimeText
            :time="item.reply.repliedAt"
            class="text-neutral-text-secondary"
            :class="sz.meta"
          />
        </div>
        <p
          ref="replyEl"
          class="mt-0.5 whitespace-pre-wrap wrap-anywhere text-neutral-text-primary"
          :class="[full ? '' : 'line-clamp-2', sz.reply]"
          v-text="item.reply.content"
        ></p>
      </div>
    </div>

    <!-- 首頁輪播：卡片底部的留白＋淡色大引號（檔頭第 13 點）。
             留白不夠高時引號不顯示（v-show 而不是 v-if：留白區一直都在，才量得到它多高）。
             -mb-8：大字的引號只畫在字框的上半部，往下拉、超出的空白由 overflow-hidden 裁掉，引號才會貼近卡片底部。 -->
    <div
      v-if="featured && !full && !item.isRatingOnly"
      ref="spacerEl"
      class="flex min-h-0 flex-1 items-end justify-end overflow-hidden"
      aria-hidden="true"
    >
      <span
        v-show="quoteFits"
        class="-mb-8 font-serif text-7xl leading-none text-brand-primary/15 select-none"
        >”</span
      >
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
