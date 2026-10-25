<!--
  MyReviewsView.vue — 會員中心「我的評論」（昱，10/10）
  路由：/member/reviews（?tab=pending 待評論、?tab=reviewed 已評論；沒給是待評論）

  運動中心改成只限會員之後，這一頁是寫評論的唯一入口：
    待評論：本人的、還沒評、還沒過期的現場和預約憑證（規格 2-9）。按「立刻評論」去撰寫頁。
    已評論：本人寫過的評論摘要（規格 2-10）。點一列展開，內容跟單筆頁一樣（MyReviewDetail）。

  ── 為什麼「已評論」展開時才讀完整內容 ─────────────────────────
  清單 API 只回摘要。展開時 MyReviewDetail 才讀完整內容，並在「真的看到館方回覆」時記錄已讀。
  如果清單一打開就讀全部，每一則有回覆的評論都會被當成「顧客看過了」，營運分析的已讀率就失真了。
  內容在「第一次展開」時才建立，所以沒展開的那幾則不會發出任何請求。

  ── 分頁記在網址上（?tab=）──────────────────────────────────
  寫完評論、從單筆頁按「回我的評論」，會回到「已評論」分頁；重新整理也停在同一個分頁。
  用 replace 換分頁：切分頁不該在瀏覽紀錄裡多一筆，否則按上一頁要按好幾次才離得開。

  ── 10/12 外觀改版（參考 Reddit／TikTok 的留言串、Airbnb 的評論列表）──────────
  1. 一張白底面板、一列一則（分隔線），取代原本「一則一張卡片」：同一畫面看得到更多則，列高也壓低了。
  2. 每列左邊一個有底色的圖示方塊：藍色＝現場評論、橘色＝預約評論、紅色＝已下架、黃色＝快到期，掃一眼就分得出來。
  3. 展開後，那一列加上藍色左邊線、展開區用淡藍底，看得出「哪裡到哪裡是這一則的詳情」。
     10/14：淡藍底從 5% 加深到 10%，原本太淡，白色的館方回覆泡泡也襯不出來。
  4. 展開區不再重複列上已經有的資訊（星等、日期、場地時段），只放完整內容、館方回覆、表態與公開切換
    （MyReviewDetail 的 compact 模式）。展開時列上的摘要那一行會藏起來，換成完整內容。
  5. 標籤只標「例外」：公開是預設，不再每列都標「公開中」，只有不公開才標；館方已回覆改成一行小字。

  ── 為什麼沒用 components/list/ListTable.vue ──────────────────
  ListTable 是「寬螢幕表格、窄螢幕卡片」的列表。這一頁不管多寬都是卡片，而且已評論要能展開，
  用表格反而要把展開的內容塞進表格列裡。標籤則用了同一組的 StatusBadge。

  ── 10/25：分頁上的數字要提醒人 ─────────────────────────────
  1. 待評論有快到期的：數字的底色變成橘色（品牌的 action 色）；沒有快到期的是白底。
     不加鬧鐘：進來預設就是待評論，下面清單的快到期那幾列已經有鬧鐘了，分頁上再放一個是重複的。
  2. 已評論有館方新回覆（顧客還沒看過）：不顯示數字，只顯示藍色的「新」；新回覆都看過之後，恢復成白底的數字。
  4. 分頁文字加大到 16px，下面的分隔線加粗到 2px。
  5. 會員中心左邊選單的「我的評論」也有提醒（components/review/MemberReviewsMarker.vue），規則寫在那個檔。
  3. 已評論清單裡，有新回覆的那幾則排到最上面（其他照原本的順序，新的在前）。
     只在讀進來的時候排一次：展開看過之後，那一則不會馬上跳回原位，免得畫面在眼前跳動；下次進來才回到原位。
-->
<script setup>
import { ref, reactive, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { listPendingReviews, listReviewedReviews } from "@/api/reviewApi";
import { useMemberStore } from "@/stores/member";
import { writeRoute } from "@/router/reviewRoutes";
import { MY_REVIEW_BADGES, TILE_TONES } from "@/constants/review";
import { kindInfo } from "@/utils/review/reviewKinds";
import { hasReviewDraft } from "@/utils/review/draftKey";
import StatusBadge from "@/components/list/StatusBadge.vue";
import StarView from "@/components/review/StarView.vue";
import MyReviewDetail from "@/components/review/MyReviewDetail.vue";

const route = useRoute();
const router = useRouter();
const memberStore = useMemberStore();

// ── 分頁 ──
const TAB_VALUES = ["pending", "reviewed"];
const tab = computed({
  // 網址上的值不認得（例如手打錯）就回到「待評論」，不要顯示一個空白的分頁
  get: () => (TAB_VALUES.includes(route.query.tab) ? route.query.tab : "pending"),
  set: (value) => router.replace({ query: { ...route.query, tab: value } }),
});

// ── 兩份清單：各自載入、各自顯示錯誤（一份失敗不影響另一份）──
const pending = ref(null); // null＝還沒載入好
const reviewed = ref(null);
const pendingError = ref("");
const reviewedError = ref("");

async function loadPending() {
  pendingError.value = "";
  try {
    const items = await listPendingReviews();
    // 「有草稿」存在這台電腦的 localStorage，後端不知道，在這裡補上
    const userId = memberStore.member?.userId;
    pending.value = items.map((item) => ({
      ...item,
      hasDraft: hasReviewDraft(userId, item.kind, item.ticket),
    }));
  } catch (e) {
    pendingError.value = e.message;
  }
}

async function loadReviewed() {
  reviewedError.value = "";
  try {
    // 10/25 原本：reviewed.value = await listReviewedReviews();
    // 有館方新回覆的排到最上面（檔頭 10/25 第 3 點）。filter 會保留原本的順序，兩段各自還是新的在前
    const items = await listReviewedReviews();
    reviewed.value = [
      ...items.filter((item) => item.hasUnviewedReply),
      ...items.filter((item) => !item.hasUnviewedReply),
    ];
  } catch (e) {
    reviewedError.value = e.message;
  }
}

// 兩份一起載入：分頁上的數字一開始就要對
onMounted(() => Promise.all([loadPending(), loadReviewed()]));

// 10/25：快到期的則數、館方新回覆的則數（檔頭 10/25）
const expiringCount = computed(
  () => pending.value?.filter((item) => item.deadline.expiringSoon).length ?? 0,
);
const newReplyCount = computed(
  () => reviewed.value?.filter((item) => item.hasUnviewedReply).length ?? 0,
);

// 分頁上的數字。還沒載入好時不顯示（undefined），不要先顯示 0 再跳成 3
// 10/25：badge 改成物件時，會蓋掉 UTabs 預設的樣子（灰色外框）。expiring／newReplies 給下面的 #trailing 用
/* 10/25 原本：
const tabs = computed(() => [
  {
    label: "待評論",
    value: "pending",
    icon: "i-lucide-pen-line",
    badge: pending.value?.length || undefined,
  },
  {
    label: "已評論",
    value: "reviewed",
    icon: "i-lucide-message-square-text",
    badge: reviewed.value?.length || undefined,
  },
]);
*/
const tabs = computed(() => [
  {
    label: "待評論",
    value: "pending",
    icon: "i-lucide-pen-line",
    badge: pending.value?.length || undefined,
    expiring: expiringCount.value,
  },
  {
    label: "已評論",
    value: "reviewed",
    icon: "i-lucide-message-square-text",
    badge: reviewed.value?.length || undefined,
    newReplies: newReplyCount.value,
  },
]);

function showPending() {
  tab.value = "pending";
}

// ── 已評論：標籤 ──
// 原本（10/10）：每列都標回覆狀態＋公開與否，四則就有八個標籤，重點反而被淹沒
// function reviewedBadges(item) {
//   if (item.isSpamMarked) return [MY_REVIEW_BADGES.spam];
//   const list = [];
//   if (item.hasUnviewedReply) list.push(MY_REVIEW_BADGES.newReply);
//   else if (item.hasReply) list.push(MY_REVIEW_BADGES.replied);
//   else list.push(MY_REVIEW_BADGES.noReply);
//   list.push(item.isPublic ? MY_REVIEW_BADGES.public : MY_REVIEW_BADGES.private);
//   return list;
// }
// 10/12：標籤只標「例外」——要顧客注意的才標。
//   被下架 → 只標「已下架」（公開、回覆對顧客都不重要了）
//   館方新回覆 → 標（顧客還沒看過）
//   不公開 → 標（公開是預設，公開就不標）
//   「館方已回覆／尚未回覆」不是例外，改成列右邊的一行小字（replyHint）
function reviewedBadges(item) {
  if (item.isSpamMarked) return [MY_REVIEW_BADGES.spam];
  const list = [];
  if (item.hasUnviewedReply) list.push(MY_REVIEW_BADGES.newReply);
  if (!item.isPublic) list.push(MY_REVIEW_BADGES.private);
  return list;
}

// 列右下的回覆狀態小字。三種情況都寫出來，不用「否則」帶過：
//   新回覆已經有標籤了，這裡不重複
function replyHint(item) {
  if (item.isSpamMarked) return null;
  if (item.hasUnviewedReply) return null;
  if (item.hasReply) return { icon: "i-lucide-reply", text: "館方已回覆" };
  if (!item.hasReply) return { icon: "i-lucide-hourglass", text: "尚未回覆" };
  return null;
}

// 列左邊圖示方塊的顏色：例外狀態（已下架、快到期）優先，其次看評論種類
function reviewedTone(item) {
  if (item.isSpamMarked) return { icon: MY_REVIEW_BADGES.spam.icon, cls: TILE_TONES.error };
  return { icon: kindInfo(item.kind).icon, cls: kindInfo(item.kind).tileClass };
}
function pendingTone(item) {
  if (item.deadline.expiringSoon)
    return { icon: MY_REVIEW_BADGES.expiringSoon.icon, cls: TILE_TONES.warning };
  return { icon: kindInfo(item.kind).icon, cls: kindInfo(item.kind).tileClass };
}

// 展開後 MyReviewDetail 改了狀態（記錄已讀、切換公開）→ 清單上的標籤跟著改，不必整份重讀
function onReplyViewed(item) {
  item.hasUnviewedReply = false;
}
function onVisibilityChanged(item, isPublic) {
  item.isPublic = isPublic;
}

// 展開過的那幾筆（第一次展開才建立內容，之後保留）
const opened = reactive(new Set());

// 每一筆的 key：種類＋憑證（同一個 ticket 數字在現場、預約兩邊可能各有一筆）
const keyOf = (item) => `${item.kind}-${item.ticket}`;
</script>

<template>
  <div>
    <h1 class="mb-6 text-2xl font-bold text-neutral-text-primary">我的評論</h1>

    <!-- content=false：分頁只負責切換，內容放在下面自己排，兩個分頁的載入與錯誤處理才看得清楚 -->
    <!-- 10/25 原本：<UTabs v-model="tab" :items="tabs" :content="false" variant="link" class="mb-4" />
         改成自己畫數字（#trailing）：快到期時數字變橘色，有新回覆時只顯示藍色的「新」（檔頭 10/25）。
         :ui：分頁文字從 14px 加大到 16px，下面的分隔線從 1px 加粗到 2px（藍色的目前分頁線跟著變 2px，才蓋得住） -->
    <UTabs
      v-model="tab"
      :items="tabs"
      :content="false"
      variant="link"
      class="mb-4"
      :ui="{
        trigger: 'text-base',
        list: 'border-b-2',
        indicator: '-bottom-0.5 h-0.5',
      }"
    >
      <template #trailing="{ item }">
        <!-- 已評論、有館方新回覆：只顯示藍色的「新」，不顯示數字；都看過之後恢復成白底的數字 -->
        <template v-if="item.newReplies">
          <UBadge
            label="新"
            color="primary"
            variant="solid"
            class="shrink-0"
            :title="`${item.newReplies} 則有館方新回覆`"
          />
          <span class="sr-only">，{{ item.newReplies }} 則有館方新回覆</span>
        </template>
        <!-- 待評論、有快到期的：數字的底色變橘色；沒有快到期的就是白底 -->
        <template v-else-if="item.badge">
          <UBadge
            :label="item.badge"
            :color="item.expiring ? 'action' : 'neutral'"
            :variant="item.expiring ? 'solid' : 'outline'"
            class="shrink-0"
            :title="item.expiring ? `其中 ${item.expiring} 則快到期` : undefined"
          />
          <span v-if="item.expiring" class="sr-only">，其中 {{ item.expiring }} 則快到期</span>
        </template>
      </template>
    </UTabs>

    <!-- ════════ 待評論 ════════ -->
    <section v-if="tab === 'pending'" role="tabpanel" aria-label="待評論">
      <UAlert
        v-if="pendingError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        :title="pendingError"
        :actions="[{ label: '重試', color: 'error', variant: 'outline', onClick: loadPending }]"
      />

      <div v-else-if="!pending" class="flex flex-col gap-3" aria-busy="true">
        <USkeleton v-for="n in 3" :key="n" class="h-16 rounded-lg" />
      </div>

      <UEmpty
        v-else-if="pending.length === 0"
        variant="outline"
        icon="i-lucide-message-square-heart"
        title="目前沒有待評論的項目"
        description="到場使用或預約付款之後，可以評論的項目會出現在這裡。"
        class="bg-neutral-surface"
      />

      <!-- 10/12：一張面板、一列一則（原本一則一張卡片，太佔高度） -->
      <ul
        v-else
        class="divide-y divide-neutral-border overflow-hidden rounded-lg border border-neutral-border bg-neutral-surface"
      >
        <li v-for="item in pending" :key="keyOf(item)" class="flex items-center gap-3 px-4 py-3">
          <!-- 圖示方塊：藍＝現場、橘＝預約、黃＝快到期 -->
          <span
            class="flex size-10 shrink-0 items-center justify-center rounded-lg"
            :class="pendingTone(item).cls"
            :title="kindInfo(item.kind).label"
          >
            <UIcon :name="pendingTone(item).icon" class="size-5" aria-hidden="true" />
            <span class="sr-only">{{ kindInfo(item.kind).label }}</span>
          </span>

          <div class="min-w-0 flex-1">
            <p class="flex min-w-0 flex-wrap items-baseline gap-x-2">
              <span class="truncate font-semibold text-neutral-text-primary">
                {{ item.context.primary ?? "（查無名稱）" }}
              </span>
              <span v-if="item.context.secondary" class="text-sm text-neutral-text-secondary">
                {{ item.context.secondary }}
              </span>
            </p>
            <p class="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
              <!-- 剩餘時間是後端用網站時間（時光機）算好的，跟能不能評的判定一致 -->
              <time
                :datetime="item.deadline.expiresAt.value"
                :title="`評論期限 ${item.deadline.expiresAt.full}`"
                :class="
                  item.deadline.expiringSoon
                    ? 'font-semibold text-semantic-warning'
                    : 'text-neutral-text-secondary'
                "
              >
                <!-- 快到期：左邊的圖示方塊已經換成黃色鬧鐘，這裡只加粗變色；讀螢幕的人另外聽到「快到期」 -->
                <span v-if="item.deadline.expiringSoon" class="sr-only">快到期，</span>
                {{ item.deadline.remainingText }}
              </time>
              <span
                v-if="item.hasDraft"
                class="inline-flex items-center gap-1 text-neutral-text-secondary"
              >
                <UIcon :name="MY_REVIEW_BADGES.hasDraft.icon" aria-hidden="true" />
                {{ MY_REVIEW_BADGES.hasDraft.label }}
              </span>
            </p>
          </div>

          <!-- 主要行動用 action 色。有草稿時改成「繼續撰寫」，顧客才知道進去會看到上次寫的內容 -->
          <UButton
            :to="writeRoute(item.kind, String(item.ticket))"
            color="action"
            size="sm"
            icon="i-lucide-pen-line"
            :label="item.hasDraft ? '繼續撰寫' : '立刻評論'"
            class="shrink-0"
          />
        </li>
      </ul>
    </section>

    <!-- ════════ 已評論 ════════ -->
    <section v-else role="tabpanel" aria-label="已評論">
      <UAlert
        v-if="reviewedError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        :title="reviewedError"
        :actions="[{ label: '重試', color: 'error', variant: 'outline', onClick: loadReviewed }]"
      />

      <div v-else-if="!reviewed" class="flex flex-col gap-3" aria-busy="true">
        <USkeleton v-for="n in 3" :key="n" class="h-16 rounded-lg" />
      </div>

      <UEmpty
        v-else-if="reviewed.length === 0"
        variant="outline"
        icon="i-lucide-message-square-text"
        title="還沒有寫過評論"
        description="寫好的評論和館方的回覆，都會整理在這裡。"
        :actions="[
          {
            label: '看看待評論',
            color: 'primary',
            variant: 'outline',
            onClick: showPending,
          },
        ]"
        class="bg-neutral-surface"
      />

      <!-- 10/12：一張面板、一列一則；展開的那一列有藍色左邊線，展開區是淡藍底 -->
      <ul
        v-else
        class="divide-y divide-neutral-border overflow-hidden rounded-lg border border-neutral-border bg-neutral-surface"
      >
        <li v-for="item in reviewed" :key="keyOf(item)">
          <!-- 預設收起；第一次展開時才建立 MyReviewDetail（才讀完整內容、才記錄已讀）。
               之後收起來也保留（unmount-on-hide=false）：記錄已讀、切換公開還在送出時就收起，
               元件被拆掉的話通知不到清單，標籤會停在舊的狀態。保留也讓再次展開時不必重讀。 -->
          <UCollapsible
            :unmount-on-hide="false"
            class="group/row"
            @update:open="(open) => open && opened.add(keyOf(item))"
          >
            <!-- 觸發區是整列。裡面不能再放按鈕（例如 TimeText），按鈕裡面放按鈕不合 HTML 規範 -->
            <button
              type="button"
              class="group flex w-full cursor-pointer items-start gap-3 border-l-[3px] border-transparent px-4 py-3 text-left transition-colors hover:bg-brand-background focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-brand-primary data-[state=open]:border-brand-primary data-[state=open]:bg-brand-primary/10"
            >
              <span
                class="flex size-10 shrink-0 items-center justify-center rounded-lg"
                :class="reviewedTone(item).cls"
                :title="kindInfo(item.kind).label"
              >
                <UIcon :name="reviewedTone(item).icon" class="size-5" aria-hidden="true" />
                <span class="sr-only">{{ kindInfo(item.kind).label }}</span>
              </span>

              <span class="block min-w-0 flex-1">
                <span class="flex min-w-0 items-baseline gap-2">
                  <span class="truncate font-semibold text-neutral-text-primary">
                    {{ item.context.primary ?? "（查無名稱）" }}
                  </span>
                  <span
                    v-if="item.context.secondary"
                    class="hidden shrink-0 text-sm text-neutral-text-secondary sm:inline"
                  >
                    {{ item.context.secondary }}
                  </span>
                  <time
                    class="ml-auto shrink-0 text-xs text-neutral-text-secondary"
                    :datetime="item.createdAt.value"
                    :title="item.createdAt.full"
                  >
                    {{ item.createdAt.ago }}
                  </time>
                </span>

                <span class="mt-1 flex min-w-0 items-center gap-2">
                  <StarView :rating="item.starRating" size-class="text-sm" />
                  <!-- 展開後這一行藏起來：下面會顯示完整內容，不重複 -->
                  <span
                    class="truncate text-sm text-neutral-text-secondary group-data-[state=open]:hidden"
                  >
                    {{ item.excerpt ?? "只給了星等" }}
                  </span>
                </span>

                <span
                  v-if="reviewedBadges(item).length || replyHint(item)"
                  class="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1"
                >
                  <StatusBadge v-for="b in reviewedBadges(item)" :key="b.label" :status="b" />
                  <!-- 展開後藏起來：下面就看得到館方回覆了 -->
                  <span
                    v-if="replyHint(item)"
                    class="inline-flex items-center gap-1 text-xs text-neutral-text-secondary group-data-[state=open]:hidden"
                  >
                    <UIcon :name="replyHint(item).icon" aria-hidden="true" />
                    {{ replyHint(item).text }}
                  </span>
                </span>
              </span>

              <UIcon
                name="i-lucide-chevron-down"
                class="mt-2.5 size-5 shrink-0 text-neutral-text-secondary transition-transform group-data-[state=open]:rotate-180"
                aria-hidden="true"
              />
            </button>

            <template #content>
              <!-- 展開區：跟上面那一列同一條藍色左邊線、同一個淡藍底，看得出這是同一則。
                   左邊縮排對齊列上的文字（圖示方塊 2.5rem＋間距 0.75rem＋列的左內距 1rem） -->
              <div
                class="border-l-[3px] border-brand-primary bg-brand-primary/10 pr-4 pb-4 pl-4 sm:pl-[4.25rem]"
              >
                <MyReviewDetail
                  v-if="opened.has(keyOf(item))"
                  compact
                  :kind="item.kind"
                  :ticket="String(item.ticket)"
                  @reply-viewed="onReplyViewed(item)"
                  @visibility-changed="(isPublic) => onVisibilityChanged(item, isPublic)"
                />
              </div>
            </template>
          </UCollapsible>
        </li>
      </ul>
    </section>
  </div>
</template>
