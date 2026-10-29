// 付款結果視窗的規則：付款狀態 → 要顯示什麼（圖示、標題、說明、有哪些按鈕）
// 從綠界付完錢回到「訂單與付款」頁時，PaymentResultModal 用這裡的規則決定畫面
// 規則跟畫面分開：要改文字、加新的付款狀態，只改這個檔案
import { PAYMENT_STATUS, PAYMENT_STATUS_CODE } from "@/constants/statuses";
import { canPay, formatRemaining } from "@/utils/order";

// 後端把客人從綠界導回前台時，網址會加上 ?from=ecpay
// ⚠️ 要跟後端 Controllers/Api/PaymentApi/EcpayPaymentApiController.cs 的 FromEcpay 一致
export const RETURN_FROM_ECPAY = Object.freeze({ key: "from", value: "ecpay" });

// 「確認中」多久重查一次、最多等多久。查的是我們自己的後端，不是綠界，不會因為查太快被綠界擋
export const PENDING_POLL_INTERVAL_MS = 5000;
export const PENDING_POLL_TIMEOUT_MS = 60000;

/**
 * 畫面的內容
 * @typedef {object} PaymentResultView
 * @property {string} icon 圖示名稱（要寫完整，Nuxt UI 打包時才掃描得到）
 * @property {string} iconClass 圖示顏色（Tailwind class，同上，要寫完整）
 * @property {string} title 標題
 * @property {string} description 說明
 * @property {boolean} isPending 還在等結果：視窗要繼續重查
 * @property {boolean} canRetry 顯示「重新付款」
 * @property {boolean} canViewDetail 顯示「查看明細」
 */

/** 沒列出的欄位用預設值：不用等、沒有按鈕 */
const view = (fields) => ({ isPending: false, canRetry: false, canViewDetail: false, ...fields });

/** 第一次向後端查到之前：列表的狀態可能已經過時，不能拿來當結果 */
export const CHECKING_VIEW = view({
  icon: "i-mdi-progress-clock",
  iconClass: "text-neutral-text-secondary",
  title: "確認付款結果中…",
  description: "正在查詢這筆訂單的付款狀態。",
  isPending: true,
});

/** 等到最後都沒查到（例如網路一直不通） */
export const UNAVAILABLE_VIEW = view({
  icon: "i-mdi-cloud-off-outline",
  iconClass: "text-neutral-text-secondary",
  title: "暫時無法確認付款結果",
  description: "請稍後重新整理頁面，查看訂單的付款狀態。",
});

/**
 * 依照後端查到的訂單，決定付款結果的畫面
 * @param {object} order 訂單（GET /api/orders/{orderId} 的明細）
 * @param {Date} now 網站時間（useSiteClock），決定還能不能重新付款
 * @param {object} [options]
 * @param {boolean} [options.timedOut] 確認中已經等超過 PENDING_POLL_TIMEOUT_MS
 * @returns {PaymentResultView}
 */
export function describePaymentResult(order, now, { timedOut = false } = {}) {
  switch (order.paymentStatus) {
    case PAYMENT_STATUS_CODE.PAID:
      return view({
        icon: "i-mdi-check-circle",
        iconClass: "text-semantic-success",
        title: "付款成功",
        description: "預約已確認，入場票券已產生。",
        canViewDetail: true,
      });

    case PAYMENT_STATUS_CODE.FAILED:
      return describeFailed(order, now);

    case PAYMENT_STATUS_CODE.PROCESSING:
      return timedOut
        ? view({
            icon: "i-mdi-progress-clock",
            iconClass: "text-semantic-warning",
            title: "付款確認中",
            description: "綠界還沒有通知付款結果。結果確認後會顯示在訂單列表，可以先關閉這個視窗。",
          })
        : view({
            icon: "i-mdi-progress-clock",
            iconClass: "text-semantic-warning",
            title: "付款確認中…",
            description: "正在等候綠界通知付款結果，通常幾秒內就會完成。",
            isPending: true,
          });

    // 付款期限過後才收到錢，而且時段已經被別人訂走（後端 PaymentConfirmationService）
    case PAYMENT_STATUS_CODE.REFUNDING:
      return view({
        icon: "i-mdi-cash-refund",
        iconClass: "text-brand-primary",
        title: "已收到款項，但預約已失效",
        description: "付款期限過後才收到款項，原本的時段已經無法保留，請到櫃台辦理退款。",
      });

    // 未付款、付款取消、已退款：這次付款沒有完成
    default:
      return view({
        icon: "i-mdi-information-outline",
        iconClass: "text-neutral-text-secondary",
        title: "沒有收到這筆付款",
        description: `目前的付款狀態是「${PAYMENT_STATUS[order.paymentStatus]?.label ?? "不明"}」。`,
        canRetry: canPay(order, now),
      });
  }
}

/** 付款失敗：這次沒有扣款。還在期限內就可以重新付款 */
function describeFailed(order, now) {
  const canRetry = canPay(order, now);
  return view({
    icon: "i-mdi-close-circle",
    iconClass: "text-semantic-error",
    title: "付款失敗",
    description: canRetry
      ? `這次沒有扣款，可以在期限內重新付款（${formatRemaining(order.paymentStartDeadline, now)}）。`
      : "這次沒有扣款。已經超過可以重新付款的時間，付款期限到了之後預約會自動取消。",
    canRetry,
  });
}
