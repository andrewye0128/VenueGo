// 列表頁「付款期限到了，自動重新讀取」：「我的預約」「訂單與付款」共用
//
// 用途：後端的逾期排程（每 1 分鐘）會把「期限過了還沒付錢」的預約改成已逾期，
//   但前端手上的是打開頁面時拿到的舊資料。這裡在「期限＋75 秒」時自動重新讀一次，
//   使用者不用自己重新整理，就會看到「已逾期」「付款取消」
//
// 用法（在頁面的 setup 裡呼叫）：
//   useDueRefresh(
//     computed(() => result.value.items),   // 這一頁目前顯示的資料
//     () => load({ silent: true }),         // 時間到要做的事：安靜地重新讀取（不顯示讀取中）
//   );
//
// 什麼時候會重新讀取：
//   1. 這一頁有「待確認、未付款或付款失敗」的資料，最快到期的那一筆期限＋75 秒時
//      重新讀取後還是待確認（排程比較慢）→ 1 分鐘後再補查，最多補 2 次，之後就不查了
//   2. 使用者切回這個分頁時（離開的這段時間可能有資料逾期，或被員工在後台改了）
//
// 為什麼用 useSiteClock 的 now 每秒檢查，不用 setTimeout：
//   setTimeout 照瀏覽器的真實時間倒數，時光機把網站時間往後調時它不會知道；
//   now 是網站時間，時光機一調就跳過去，這裡馬上發現「時間到了」
import { onMounted, onUnmounted, watch } from "vue";
import { useSiteClock } from "@/composables/useSiteClock";
import { showsDueAt } from "@/utils/order";

// 期限過後多久再查：後端逾期排程每 1 分鐘跑一次，60 秒＋15 秒緩衝
const GRACE_MS = 75 * 1000;
// 查完還是待確認時，隔多久再補查、最多補幾次
const RETRY_MS = 60 * 1000;
const MAX_RETRIES = 2;
// 剛讀完資料的這段時間內不重讀：避免一打開頁面就因為「早就過期的資料」馬上又讀一次，
// 或使用者快速切換分頁時一直重讀
const MIN_GAP_MS = 10 * 1000;

/**
 * @param {import("vue").Ref<object[]>} items 這一頁目前顯示的資料（每筆要有 reservationId、
 *   reservationStatus、paymentStatus、paymentDueAt；我的預約、訂單與付款的 API 都有）
 * @param {() => Promise<void>} refresh 重新讀取的函式
 */
export function useDueRefresh(items, refresh) {
  const { now } = useSiteClock();

  // 每一筆已經因為到期查過幾次（key：reservationId）。補查超過次數的就不再管它
  const tries = new Map();
  let refreshing = false;
  // 最後一次拿到資料的時間（用 performance.now()，是真實經過的時間，不受時光機影響）
  let loadedAt = performance.now();

  // 資料換了（不管是誰讀的：使用者查詢、換頁、這裡自動讀）就記下時間
  watch(items, () => {
    loadedAt = performance.now();
  });

  // 這一筆「下次該查的時間」：期限＋75 秒，每補查一次再往後 1 分鐘；不用再管的回傳 null
  function nextCheckAt(item) {
    // 待確認、未付款或付款失敗才可能變成逾期（同後端排程的條件；規則在 utils/order.js）
    if (!showsDueAt(item) || !item.paymentDueAt) return null;
    const count = tries.get(item.reservationId) ?? 0;
    if (count > MAX_RETRIES) return null;
    return new Date(item.paymentDueAt).getTime() + GRACE_MS + count * RETRY_MS;
  }

  async function run() {
    if (refreshing) return;
    refreshing = true;
    try {
      await refresh();
    } finally {
      refreshing = false;
    }
  }

  // 每秒（網站時間每走一秒）檢查一次：有沒有哪一筆已經到了該查的時間
  watch(now, (current) => {
    if (refreshing || performance.now() - loadedAt < MIN_GAP_MS) return;

    const due = items.value.filter((item) => {
      const at = nextCheckAt(item);
      return at !== null && at <= current.getTime();
    });
    if (due.length === 0) return;

    // 先記下「查過一次」，讀回來還是待確認的話，下次就會晚 1 分鐘再查
    for (const item of due) {
      tries.set(item.reservationId, (tries.get(item.reservationId) ?? 0) + 1);
    }
    run();
  });

  // 切回這個分頁時重新讀取
  function onVisibilityChange() {
    if (document.visibilityState !== "visible") return;
    if (performance.now() - loadedAt < MIN_GAP_MS) return;
    run();
  }
  onMounted(() => document.addEventListener("visibilitychange", onVisibilityChange));
  onUnmounted(() => document.removeEventListener("visibilitychange", onVisibilityChange));
}
