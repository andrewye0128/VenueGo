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

  ── 為什麼沒用 components/list/ListTable.vue ──────────────────
  ListTable 是「寬螢幕表格、窄螢幕卡片」的列表。這一頁不管多寬都是卡片，而且已評論要能展開，
  用表格反而要把展開的內容塞進表格列裡。標籤則用了同一組的 StatusBadge。
-->
<script setup>
import { ref, reactive, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { listPendingReviews, listReviewedReviews } from "@/api/reviewApi";
import { useMemberStore } from "@/stores/member";
import { writeRoute } from "@/router/reviewRoutes";
import { MY_REVIEW_BADGES } from "@/constants/review";
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
    reviewed.value = await listReviewedReviews();
  } catch (e) {
    reviewedError.value = e.message;
  }
}

// 兩份一起載入：分頁上的數字一開始就要對
onMounted(() => Promise.all([loadPending(), loadReviewed()]));

// 分頁上的數字。還沒載入好時不顯示（undefined），不要先顯示 0 再跳成 3
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

function showPending() {
  tab.value = "pending";
}

// ── 已評論：標籤 ──
// 被下架時只顯示「已下架」：公開與否、回覆狀態對顧客都不重要了，標籤太多反而看不到重點
function reviewedBadges(item) {
  if (item.isSpamMarked) return [MY_REVIEW_BADGES.spam];
  const list = [];
  if (item.hasUnviewedReply) list.push(MY_REVIEW_BADGES.newReply);
  else if (item.hasReply) list.push(MY_REVIEW_BADGES.replied);
  else list.push(MY_REVIEW_BADGES.noReply);
  list.push(item.isPublic ? MY_REVIEW_BADGES.public : MY_REVIEW_BADGES.private);
  return list;
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
    <UTabs v-model="tab" :items="tabs" :content="false" variant="link" class="mb-4" />

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
        <USkeleton v-for="n in 3" :key="n" class="h-28 rounded-lg" />
      </div>

      <UEmpty
        v-else-if="pending.length === 0"
        variant="outline"
        icon="i-lucide-message-square-heart"
        title="目前沒有待評論的項目"
        description="到場使用或預約付款之後，可以評論的項目會出現在這裡。"
        class="bg-neutral-surface"
      />

      <ul v-else class="flex flex-col gap-3">
        <li
          v-for="item in pending"
          :key="keyOf(item)"
          class="flex flex-col gap-3 rounded-lg border border-neutral-border bg-neutral-surface p-4 md:flex-row md:items-center md:justify-between md:p-6"
        >
          <div class="min-w-0">
            <p class="mb-1 flex items-center gap-1 text-sm text-neutral-text-secondary">
              <UIcon :name="kindInfo(item.kind).icon" aria-hidden="true" />
              {{ kindInfo(item.kind).label }}
            </p>
            <p class="truncate font-semibold text-neutral-text-primary">
              {{ item.context.primary ?? "（查無名稱）" }}
            </p>
            <p v-if="item.context.secondary" class="text-sm text-neutral-text-secondary">
              {{ item.context.secondary }}
            </p>

            <div class="mt-2 flex flex-wrap items-center gap-2">
              <StatusBadge
                v-if="item.deadline.expiringSoon"
                :status="MY_REVIEW_BADGES.expiringSoon"
              />
              <!-- 剩餘時間是後端用網站時間（時光機）算好的，跟能不能評的判定一致 -->
              <span
                class="text-sm"
                :class="
                  item.deadline.expiringSoon
                    ? 'font-semibold text-semantic-warning'
                    : 'text-neutral-text-secondary'
                "
              >
                <time
                  :datetime="item.deadline.expiresAt.value"
                  :title="`評論期限 ${item.deadline.expiresAt.full}`"
                >
                  {{ item.deadline.remainingText }}
                </time>
              </span>
              <StatusBadge v-if="item.hasDraft" :status="MY_REVIEW_BADGES.hasDraft" />
            </div>
          </div>

          <!-- 主要行動用 action 色。有草稿時改成「繼續撰寫」，顧客才知道進去會看到上次寫的內容 -->
          <UButton
            :to="writeRoute(item.kind, String(item.ticket))"
            color="action"
            icon="i-lucide-pen-line"
            :label="item.hasDraft ? '繼續撰寫' : '立刻評論'"
            class="md:shrink-0"
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
        <USkeleton v-for="n in 3" :key="n" class="h-28 rounded-lg" />
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

      <ul v-else class="flex flex-col gap-3">
        <li v-for="item in reviewed" :key="keyOf(item)">
          <!-- 預設收起；第一次展開時才建立 MyReviewDetail（才讀完整內容、才記錄已讀）。
               之後收起來也保留（unmount-on-hide=false）：記錄已讀、切換公開還在送出時就收起，
               元件被拆掉的話通知不到清單，標籤會停在舊的狀態。保留也讓再次展開時不必重讀。 -->
          <UCollapsible
            :ui="{ content: 'mt-2' }"
            :unmount-on-hide="false"
            @update:open="(open) => open && opened.add(keyOf(item))"
          >
            <!-- 觸發區是整張摘要卡。裡面不能再放按鈕（例如 TimeText），按鈕裡面放按鈕不合 HTML 規範 -->
            <button
              type="button"
              class="group w-full cursor-pointer rounded-lg border border-neutral-border bg-neutral-surface p-4 text-left transition-colors hover:border-brand-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary md:p-6"
            >
              <span class="flex items-start justify-between gap-3">
                <span class="block min-w-0">
                  <span class="mb-1 flex items-center gap-1 text-sm text-neutral-text-secondary">
                    <UIcon :name="kindInfo(item.kind).icon" aria-hidden="true" />
                    {{ kindInfo(item.kind).label }}
                  </span>
                  <span class="block truncate font-semibold text-neutral-text-primary">
                    {{ item.context.primary ?? "（查無名稱）" }}
                    <span
                      v-if="item.context.secondary"
                      class="ml-1 text-sm font-normal text-neutral-text-secondary"
                    >
                      {{ item.context.secondary }}
                    </span>
                  </span>
                </span>
                <UIcon
                  name="i-lucide-chevron-down"
                  class="mt-1 size-5 shrink-0 text-neutral-text-secondary transition-transform group-data-[state=open]:rotate-180"
                  aria-hidden="true"
                />
              </span>

              <span class="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1">
                <StarView :rating="item.starRating" />
                <time class="text-sm text-neutral-text-secondary" :datetime="item.createdAt.value">
                  {{ item.createdAt.full }}
                </time>
              </span>

              <span class="mt-2 block truncate text-sm text-neutral-text-secondary">
                {{ item.excerpt ?? "（只給了星等，沒有留下文字）" }}
              </span>

              <span class="mt-3 flex flex-wrap gap-2">
                <StatusBadge v-for="b in reviewedBadges(item)" :key="b.label" :status="b" />
              </span>
            </button>

            <template #content>
              <MyReviewDetail
                v-if="opened.has(keyOf(item))"
                :kind="item.kind"
                :ticket="String(item.ticket)"
                @reply-viewed="onReplyViewed(item)"
                @visibility-changed="(isPublic) => onVisibilityChanged(item, isPublic)"
              />
            </template>
          </UCollapsible>
        </li>
      </ul>
    </section>
  </div>
</template>
