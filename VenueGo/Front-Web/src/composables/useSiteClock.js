// ────────────────────────────────────────────────────────────────
//  useSiteClock：前台的「現在時間」，開發時會跟著後端的時光機走（昱）
//
//  用法：
//    const { now, traveling, offsetText } = useSiteClock();
//    // now 是 ref(Date)，每秒更新；畫面上要顯示「現在」或算倒數都用它，不要自己 new Date()
//
//  ── 時間從哪裡來 ────────────────────────────────────────
//  開發（npm run dev）且後端有開：向 /api/dev/time 問網站時間（ITimeService.Now），
//    之後在瀏覽器自己往前走，每 15 秒、切回分頁時再對一次。時光機旅行中，這裡就是旅行後的時間。
//  正式版本、或後端沒開：就是瀏覽器的時鐘（new Date()），跟原本 TopBar 一樣。
//
//  ── 別的視窗調整時光機，這裡怎麼知道 ──────────────────────
//  1. 時光機小視窗（後端網址）調整後，會用 postMessage 通知打開它的頁面
//  2. 其他前台分頁調整後，用 BroadcastChannel 通知（同一個網址開頭才收得到）
//  3. 切回分頁、視窗取得焦點時問一次；保險起見每 15 秒問一次
//
//  所有元件共用同一份狀態（寫在函式外面），TopBar 和其他頁面拿到的時間一定一樣。
// ────────────────────────────────────────────────────────────────
import { ref, computed, readonly, onMounted, onUnmounted, getCurrentInstance } from "vue";

const API = "/api/dev/time";
const CHANNEL_NAME = "venuego-time-machine";
const MESSAGE_TYPE = "venuego:time-machine-changed";
const POPUP_NAME = "venuego-time-machine";
const POLL_MS = 15000;

// ── 全部元件共用的狀態 ─────────────────────────────────────
const state = ref(null); // 後端的時光機狀態；null＝沒有時光機（正式版本、後端沒開、不是開發環境）
const now = ref(new Date());
let receivedAt = 0; // 收到 state 的那一刻（performance.now()），讓時鐘自己往前走
let users = 0; // 有幾個元件在用；0 的時候把計時器都停掉
let tickTimer = null;
let pollTimer = null;
let channel = null;
let popup = null;
let lastFetchAt = 0;

/** 後端給的是台北時間字串（不帶時區），換成「各欄位等於那個時間」的 Date。 */
function parseSiteTime(text) {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})/.exec(text ?? "");
  return m ? new Date(+m[1], +m[2] - 1, +m[3], +m[4], +m[5], +m[6]) : null;
}

/** 伺服器時間＋收到之後經過的時間。 */
function live(text) {
  const base = parseSiteTime(text);
  return base ? new Date(base.getTime() + (performance.now() - receivedAt)) : null;
}

function tick() {
  now.value = (state.value && live(state.value.siteNow)) || new Date();
}

function setState(next) {
  state.value = next?.available ? next : null;
  receivedAt = performance.now();
  tick();
}

async function request(method, path = "", body) {
  const init = {
    method,
    cache: "no-store",
    headers: { Accept: "application/json" },
  };
  if (body !== undefined) {
    init.headers["Content-Type"] = "application/json";
    init.body = JSON.stringify(body);
  }
  const response = await fetch(API + path, init);
  let json = null;
  try {
    json = await response.json();
  } catch {
    json = null;
  }
  if (!response.ok || !json?.success) {
    throw new Error(json?.message || `時光機沒有回應（HTTP ${response.status}）`);
  }
  return json;
}

/** 向後端問一次。失敗（後端沒開、正在重新啟動）就保留原本的狀態，不打擾使用者。 */
async function refresh() {
  if (!import.meta.env.DEV) return;
  lastFetchAt = performance.now();
  try {
    const json = await request("GET");
    setState(json.data);
  } catch {
    // try/catch 的決定：問不到很正常（後端沒開、正在重新啟動），安靜地等下一次
  }
}

/** 自己調整了：更新畫面，並通知其他前台分頁、時光機小視窗。 */
function afterLocalChange(json) {
  setState(json.data);
  channel?.postMessage({ type: MESSAGE_TYPE });
  try {
    if (popup && !popup.closed) popup.postMessage({ type: MESSAGE_TYPE }, "*");
  } catch {
    // 小視窗已經關掉或換頁：不影響這邊
  }
  return json;
}

// ── 事件 ───────────────────────────────────────────────────
// 任何網頁都能 postMessage 給我們，所以收到「有變」只當作提醒，內容一律自己去問後端
const onMessage = (event) => {
  if (event.data?.type === MESSAGE_TYPE) refresh();
};
const onVisible = () => {
  if (!document.hidden) refresh();
};
const onFocus = () => {
  if (performance.now() - lastFetchAt > 2000) refresh();
};

function start() {
  tickTimer = setInterval(tick, 1000);
  if (!import.meta.env.DEV) return;

  refresh();
  pollTimer = setInterval(() => {
    if (!document.hidden) refresh();
  }, POLL_MS);
  window.addEventListener("message", onMessage);
  document.addEventListener("visibilitychange", onVisible);
  window.addEventListener("focus", onFocus);
  try {
    channel = new BroadcastChannel(CHANNEL_NAME);
    channel.onmessage = onMessage;
  } catch {
    channel = null; // 很舊的瀏覽器沒有 BroadcastChannel，靠輪詢就好
  }
}

function stop() {
  clearInterval(tickTimer);
  clearInterval(pollTimer);
  window.removeEventListener("message", onMessage);
  document.removeEventListener("visibilitychange", onVisible);
  window.removeEventListener("focus", onFocus);
  channel?.close();
  channel = null;
}

// ── 給元件用的 ─────────────────────────────────────────────

/** 有沒有時光機可以用（開發環境、後端有開）。 */
const available = computed(() => !!state.value);

/** 時光機是不是旅行中。 */
const traveling = computed(() => !!state.value?.traveling);

/** 「快 22 天 17 小時」；沒在旅行是空字串。 */
const offsetText = computed(() => state.value?.offsetText ?? "");

/** 「回到現在」之後會變成的時間（真實時間）。 */
const realNow = computed(() => {
  void now.value; // 讓它跟著每秒更新
  return (state.value && live(state.value.realNow)) || new Date();
});

/** 用小視窗打開時光機面板（後端的 /DevTools/Time）。已經開著就拉到前面。 */
function openPanel() {
  const url = state.value?.panelUrl;
  if (!url) return;
  if (popup && !popup.closed) {
    popup.focus();
    return;
  }
  const width = 460;
  const height = 720;
  const left = Math.max(0, window.screenX + window.outerWidth - width - 24);
  const top = Math.max(0, window.screenY + 60);
  popup = window.open(
    url,
    POPUP_NAME,
    `popup=yes,width=${width},height=${height},left=${left},top=${top}`,
  );
  if (!popup) window.open(url, "_blank"); // 小視窗被瀏覽器擋掉時，改開新分頁
}

/** 回到真實時間。失敗會丟出 Error（message 是給人看的原因），由呼叫端決定怎麼顯示。 */
function resetToNow() {
  return request("POST", "/reset", {}).then(afterLocalChange);
}

export function useSiteClock() {
  // 在元件裡呼叫才掛生命週期；第一個元件掛上時開始，最後一個卸下時停止
  if (getCurrentInstance()) {
    onMounted(() => {
      if (users++ === 0) start();
    });
    onUnmounted(() => {
      if (--users === 0) stop();
    });
  }

  return {
    now: readonly(now),
    available,
    traveling,
    offsetText,
    realNow,
    openPanel,
    resetToNow,
    refresh,
  };
}
