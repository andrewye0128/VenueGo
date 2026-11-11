// ────────────────────────────────────────────────────────────────
//  會員即時通知：連上後端的 MemberNotificationHub（SignalR），後端有消息時立刻通知頁面
//
//  用法：
//    1. App.vue（已經寫好）：登入中就連線、登出就斷線
//         watch(() => memberStore.isLoggedIn, setMemberNotificationsEnabled, { immediate: true });
//    2. 頁面（在 setup 裡呼叫，跟 onMounted 一樣）：預約有變化時重新讀取
//         onReservationChanged((change) => {
//           if (change.affects(畫面上的預約 Id 清單)) load();
//         });
//         change.reservationIds：哪幾筆預約變了；null＝可能漏掉了通知（剛重新連上），全部都要重新讀
//         change.affects(ids)：畫面上有沒有受影響的預約（null 的時候一律是 true）
//
//  ── 通知只說「有變化」，不帶新狀態 ─────────────────────────
//  收到通知後，頁面一律再呼叫原本的 API 讀最新資料，畫面的資料只有 API 一個來源
//  （後端也只送 reservationId，說明見後端 Hubs/MemberNotificationHub.cs）
//
//  ── 0.3 秒內的通知合併成一次 ──────────────────────────────
//  逾期排程一輪可能處理同一位會員的好幾筆，通知會幾乎同時連續送來。
//  如果每則都通知頁面，頁面會連續重新讀取好幾次（結果都一樣）。
//  所以收到第一則後等 0.3 秒，這段時間內的通知合併成「這幾筆有變化」，頁面只要讀一次
//
//  ── 斷線怎麼辦 ────────────────────────────────────────────
//  - 連上之後斷線：SignalR 的 withAutomaticReconnect 會自己重連（0、2、10、30 秒後各試一次）
//    重新連上後通知頁面全部重新讀一次，補回斷線期間漏掉的變化
//  - 第一次就連不上、或自動重連也放棄了：這裡自己隔一段時間再試，直到連上或登出
//  - 完全連不上（例如網路擋掉 WebSocket）：頁面還有 useDueRefresh（期限＋75 秒自動再讀）當備案
//
//  ── 為什麼 token 放在網址上 ────────────────────────────────
//  瀏覽器建立 WebSocket 時不能自訂標頭，SignalR 會把 accessTokenFactory 給的 token 放在 ?access_token=
//  （只有建立 WebSocket 那一次；先送的 negotiate 請求還是放在 Authorization 標頭）
//  後端只有這個網址才接受網址上的 token，安全上的說明見後端 ReservationModuleExtensions_F3.cs
//
//  所有頁面共用同一條連線（寫在函式外面），同時開好幾個頁面也只連一次
// ────────────────────────────────────────────────────────────────
import { onMounted, onUnmounted } from "vue";
import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";

// 跟後端 MemberNotificationHub 的 Path、ReservationChangedEvent 一致
const HUB_PATH = "/hubs/member-notifications";
const RESERVATION_CHANGED_EVENT = "ReservationChanged";

// 自己重新連線的間隔：第 1、2、3 次分別等 2、10、30 秒，之後每 60 秒試一次
const RETRY_DELAYS_MS = [2000, 10000, 30000];
const RETRY_MAX_DELAY_MS = 60000;

// 收到第一則通知後，等多久再通知頁面（這段時間內的通知合併成一次）
// 0.3 秒：人感覺不出延遲，又足夠收齊同一輪排程連續送來的通知
const BATCH_DELAY_MS = 300;

// ── 全部頁面共用的狀態 ─────────────────────────────────────
let connection = null;
let enabled = false; // 目前應該連線嗎（登入中）
let retryTimer = null;
let retryCount = 0; // 連續失敗幾次；連上後歸零
const reservationListeners = new Set();

// 等待合併中的通知
let batchTimer = null;
let batchIds = new Set(); // 這一批變化的預約 Id
let batchMaybeMissed = false; // 這一批裡有沒有「可能漏掉了通知」

/** 把這一批通知交給所有正在聽的頁面。一個頁面出錯不影響其他頁面 */
function flushBatch() {
  const reservationIds = batchMaybeMissed ? null : [...batchIds];
  const change = Object.freeze({
    reservationIds,
    // 畫面上的這些預約有沒有受影響；可能漏掉了通知時一律當作有
    affects: (ids) => reservationIds === null || ids.some((id) => reservationIds.includes(id)),
  });
  clearBatch();

  for (const listener of reservationListeners) {
    try {
      listener(change);
    } catch (error) {
      console.error("處理預約變化通知時發生錯誤", error);
    }
  }
}

function clearBatch() {
  clearTimeout(batchTimer);
  batchTimer = null;
  batchIds = new Set();
  batchMaybeMissed = false;
}

/**
 * 收到一則變化，先放進這一批，0.3 秒後一起通知頁面
 * @param {number | null} reservationId null＝可能漏掉了通知（剛重新連上），頁面要全部重新讀
 */
function queueReservationChanged(reservationId) {
  if (reservationId === null) batchMaybeMissed = true;
  else batchIds.add(reservationId);
  batchTimer ??= setTimeout(flushBatch, BATCH_DELAY_MS);
}

function createConnection() {
  const conn = new HubConnectionBuilder()
    .withUrl(HUB_PATH, { accessTokenFactory: () => localStorage.getItem("token") ?? "" })
    .withAutomaticReconnect()
    // 開發時只顯示警告以上的訊息；正式版本不印（斷線重連是正常狀況，不用嚇到使用者）
    .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.None)
    .build();

  conn.on(RESERVATION_CHANGED_EVENT, (payload) =>
    queueReservationChanged(payload?.reservationId ?? null),
  );
  // 重新連上：斷線期間的通知收不到，請頁面全部重新讀
  conn.onreconnected(() => queueReservationChanged(null));
  // 自動重連也放棄了，或後端關站：還在登入中就自己再試（登出時是我們自己 stop 的，不用再連）
  conn.onclose(() => {
    if (enabled) scheduleRetry();
  });
  return conn;
}

async function connect() {
  clearTimeout(retryTimer);
  if (!enabled) return;

  connection ??= createConnection();
  if (connection.state !== HubConnectionState.Disconnected) return;

  try {
    await connection.start();
    // 不是第一次就連上：失敗的這段期間可能有通知沒收到
    if (retryCount > 0) queueReservationChanged(null);
    retryCount = 0;
  } catch {
    // token 過期、後端沒開、網路斷線：等一下再試
    // 連線到一半就登出（disconnect 中斷了 start）也會走到這裡，這時不用再試
    if (enabled) scheduleRetry();
  }
}

function scheduleRetry() {
  clearTimeout(retryTimer);
  const delay = RETRY_DELAYS_MS[retryCount] ?? RETRY_MAX_DELAY_MS;
  retryCount++;
  retryTimer = setTimeout(connect, delay);
}

async function disconnect() {
  clearTimeout(retryTimer);
  retryCount = 0;
  clearBatch(); // 登出了，還沒送出的通知也不用送了
  const conn = connection;
  connection = null;
  try {
    await conn?.stop();
  } catch {
    // 本來就沒連上：不用處理
  }
}

// ── 給 App.vue、頁面用的 ─────────────────────────────────────

/**
 * 開始或停止即時通知（App.vue 依登入狀態呼叫）
 * @param {boolean} value true＝登入中，連線；false＝登出，斷線
 */
export function setMemberNotificationsEnabled(value) {
  enabled = value;
  if (value) connect();
  else disconnect();
}

/**
 * 預約有變化時要做的事（在頁面的 setup 裡呼叫；頁面離開時自動取消）
 * 0.3 秒內的通知會合併成一次呼叫（說明見檔頭）
 * @param {(change: {
 *   reservationIds: number[] | null,
 *   affects: (ids: number[]) => boolean,
 * }) => void} callback
 *   reservationIds：哪幾筆預約變了；null＝可能漏掉了通知，請全部重新讀一次
 *   affects(ids)：傳入畫面上的預約 Id，回傳有沒有受影響（null 的時候一律是 true）
 */
export function onReservationChanged(callback) {
  onMounted(() => reservationListeners.add(callback));
  onUnmounted(() => reservationListeners.delete(callback));
}
