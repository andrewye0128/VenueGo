import { ref, readonly } from "vue";
import { isNavigationFailure, NavigationFailureType } from "vue-router";

// ════════════════════════════════════════════════════════════════
//  換頁中的狀態：給 components/NavigationLoading.vue 顯示進度條
//
//  換頁時要先等路由守衛（例如確認登入狀態）、第一次打開某一頁還要下載那一頁的程式，
//  網路慢或後端沒回應時會停頓好幾秒，沒有提示的話客人會以為「按了沒反應」。
//
//  - router/index.js 呼叫 trackNavigationLoading(router)，一定要在路由守衛「之前」，
//    換頁一開始就開始計時
//  - 超過 SHOW_AFTER_MS 才算「載入中」：平常換頁不到 0.1 秒，一閃而過的進度條反而干擾
// ════════════════════════════════════════════════════════════════

// 換頁超過多久才顯示（毫秒）
const SHOW_AFTER_MS = 300;

// 是否顯示「載入中」（整個網站共用同一份）
const isLoading = ref(false);

// 目前正在進行的那一次換頁（最早要去的頁面）；沒有在換頁時是 null
let currentTarget = null;
// 「超過 SHOW_AFTER_MS 就顯示」的計時器
let showTimer = null;

// 這次換頁「最早要去的頁面」
// 路由守衛把人帶去別頁（例如帶去登入頁）時，vue-router 會換一份新的頁面資料，
// 但 redirectedFrom 會記著最早那一頁，所以用它來認「是不是同一次換頁」
const originOf = (to) => to.redirectedFrom ?? to;

// 換頁開始：重新計時。上一次還沒結束就又換頁（客人等不及點了別的連結），以新的這次為準
// 被守衛帶去別頁時也會再走到這裡，originOf 一樣，計時不會被打斷
function start(to) {
  if (originOf(to) === currentTarget) return;
  currentTarget = originOf(to);
  clearTimeout(showTimer);
  showTimer = setTimeout(() => {
    isLoading.value = true;
  }, SHOW_AFTER_MS);
}

// 換頁結束（成功、被擋下、出錯都算）：只有「目前這一次」結束才收起來
// 被新的換頁取代的舊換頁也會走到這裡，這時不能把新換頁的進度條收掉
// 例外：等待中客人點了「現在正在看的這一頁」（duplicated），vue-router 不會通知換頁開始，
// 但這個動作已經取消了原本慢的那次換頁，畫面也不會再變，所以直接收起來
function finish(to, failure) {
  const isDuplicated = isNavigationFailure(failure, NavigationFailureType.duplicated);
  if (!isDuplicated && originOf(to) !== currentTarget) return;
  currentTarget = null;
  clearTimeout(showTimer);
  isLoading.value = false;
}

// 讓路由器在換頁開始、結束、出錯時通知這裡
export function trackNavigationLoading(router) {
  router.beforeEach((to) => {
    start(to);
  });
  router.afterEach((to, from, failure) => finish(to, failure));
  router.onError((error, to) => finish(to));
}

// 給畫面讀取：isLoading 是唯讀的，只能由換頁的流程改變
export function useNavigationLoading() {
  return { isLoading: readonly(isLoading) };
}
