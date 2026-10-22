// 訂單與付款的共用規則：表格、手機卡片、明細視窗都用這裡的函式，規則只寫一次
import { PAYMENT_METHOD } from "@/constants/payment";

// 可以付款的付款狀態：0 未付款、3 付款失敗（失敗後可以重新付款）
const PAYABLE_PAYMENT_STATUSES = [0, 3];
// 預約要還在「待確認」（0）：已取消、已逾期就不能付款
const PAYABLE_RESERVATION_STATUS = 0;

// "2026-10-15T07:21:00"（沒有時區）→ Date
// 有日期也有時間、沒有時區的字串，JavaScript 會當成本地時間（台灣時間），所以可以直接 new Date
const parseApiDateTime = (value) => (value ? new Date(value) : null);

/**
 * 能不能付款（決定要不要顯示「付款」按鈕）
 * paymentStartDeadline（最晚可以開始付款的時間）由後端計算：期限前幾分鐘就不能開始付款，
 * 分鐘數在後端設定檔，前端不寫死數字
 * ⚠️ 這裡只決定「要不要顯示按鈕」，按下付款時後端會再檢查一次
 * @param {object} order 訂單（列表或明細的資料）
 * @param {Date} now 現在時間：頁面請傳 useSiteClock() 的網站時間，開發時才會跟後端的時光機一致
 */
export function canPay(order, now = new Date()) {
  if (order.paymentMethod !== PAYMENT_METHOD.ONLINE) return false;
  if (!PAYABLE_PAYMENT_STATUSES.includes(order.paymentStatus)) return false;
  if (order.reservationStatus !== PAYABLE_RESERVATION_STATUS) return false;
  const deadline = parseApiDateTime(order.paymentStartDeadline);
  return deadline !== null && deadline > now;
}

/**
 * 距離「最晚可以開始付款」還剩多久 → "剩 23 分鐘"、"剩 1 小時 5 分"
 * 不到 1 分鐘也顯示「剩 1 分鐘」：無條件進位，避免顯示「剩 0 分鐘」按鈕卻還能按
 */
export function formatRemaining(deadline, now = new Date()) {
  const end = parseApiDateTime(deadline);
  if (!end) return "";
  const minutes = Math.max(Math.ceil((end - now) / 60000), 0);
  if (minutes < 60) return `剩 ${minutes} 分鐘`;
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest === 0 ? `剩 ${hours} 小時` : `剩 ${hours} 小時 ${rest} 分`;
}

/**
 * 要不要顯示付款期限：還沒付款、預約還有效時才需要知道期限
 * （已付款顯示付款時間；已取消、已逾期就沒有期限可言）
 */
export function showsDueAt(order) {
  return (
    PAYABLE_PAYMENT_STATUSES.includes(order.paymentStatus) &&
    order.reservationStatus === PAYABLE_RESERVATION_STATUS
  );
}
