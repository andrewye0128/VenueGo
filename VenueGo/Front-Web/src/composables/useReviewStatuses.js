// 我的預約、我的票券上的評論按鈕（昱，10/25）
// 後端：[GET] /api/reviews/mine/status（規格 2-12），一筆訂單一列：
//   { orderId, reservationId, bookingAction, visitAction, visitTicketId }
//   action："write"＝立刻評論、"view"＝查看評論、null＝不顯示
//
// 用法：
//   const reviewStatus = useReviewStatuses();
//   reviewStatus.bookingFor(reservation.reservationId)  // 預約評論的按鈕，沒有就是 null
//   reviewStatus.visitFor(ticket.id)                     // 現場評論的按鈕，沒有就是 null
//   兩個都回傳 { action, label, to, color, variant }：label 是按鈕文字，to 是 RouterLink／router.push 用的路由位置，
//   color、variant 直接給 UButton。
//
// ── 按鈕的顏色（10/25）──
// 立刻評論＝橘色實心（color "action"、variant "solid"）：要顧客去做的事，跟「我的評論」頁的「立刻評論」同一個樣子。
// 查看評論＝藍色外框（color "primary"、variant "outline"）：只是去看，跟各頁的「查看」同一個樣子。
// 三個地方都用這兩組，按鈕長得一樣，顧客一看就知道是同一件事。
//
// ── 為什麼做成共用的 composable ──
// 按鈕放在三個地方：我的預約（表格）、預約明細視窗、我的票券（每張票一個 TicketCard）。
// 票券頁有幾張票就有幾個 TicketCard，各自呼叫 API 的話一進頁面就打好幾次。
// 所以清單放在模組層級共用：同一時間只會有一個請求，大家等同一個結果。
// 3 秒內已經讀過也直接用（例如表格讀完之後馬上打開明細視窗）；超過 3 秒就重讀，
// 寫完評論回到我的預約時，按鈕才會從「立刻評論」變成「查看評論」。
//
// 讀不到（沒登入、後端出錯）就當成沒有按鈕，不跳錯誤：評論按鈕只是附加的入口，不能讓預約、票券頁跟著壞掉。
import { ref } from "vue";
import { listMyReviewStatuses } from "@/api/reviewApi";
import { writeRoute, mineRoute } from "@/router/reviewRoutes";

const REUSE_MS = 3000;

const statuses = ref([]);
let inflight = null;
let loadedAt = 0;

function load() {
  if (inflight) return inflight;
  if (Date.now() - loadedAt < REUSE_MS) return Promise.resolve();
  inflight = listMyReviewStatuses()
    .then((data) => {
      statuses.value = Array.isArray(data) ? data : [];
      loadedAt = Date.now();
    })
    .catch(() => {
      statuses.value = []; // 讀不到就不顯示按鈕（檔頭）
    })
    .finally(() => {
      inflight = null;
    });
  return inflight;
}

function button(kind, action, orderId) {
  if (action === "write")
    return {
      action,
      label: "立刻評論",
      to: writeRoute(kind, String(orderId)),
      color: "action",
      variant: "solid",
    };
  if (action === "view")
    return {
      action,
      label: "查看評論",
      to: mineRoute(kind, String(orderId)),
      color: "primary",
      variant: "outline",
    };
  return null;
}

export function useReviewStatuses() {
  load();

  /** 預約評論的按鈕：用 reservationId 對（我的預約的每一列、預約明細視窗） */
  function bookingFor(reservationId) {
    const s = statuses.value.find((x) => x.reservationId === reservationId);
    return s ? button("booking", s.bookingAction, s.orderId) : null;
  }

  /** 現場評論的按鈕：用票券 Id 對。一筆訂單有好幾張票時，只有 visitTicketId 那一張有按鈕（每筆訂單只能評一次） */
  function visitFor(ticketId) {
    const s = statuses.value.find((x) => x.visitTicketId === ticketId);
    return s ? button("visit", s.visitAction, s.orderId) : null;
  }

  return { bookingFor, visitFor, reload: load };
}
