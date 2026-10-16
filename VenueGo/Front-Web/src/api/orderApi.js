// 訂單與付款 API（會員中心「訂單與付款」頁）
// 不用自己帶會員資訊：http.js 會自動在標頭加上登入的 JWT，後端從 token 知道是哪位會員
// ⚠️ 後端還沒做：先回傳假資料（mocks/myOrders.js），後端做好後換成註解裡的寫法
import { ALL } from "@/constants/statuses";
import { myOrders } from "@/mocks/myOrders";
import { delay } from "@/utils/delay";

// 列表不回傳的欄位（明細才有）：假資料要拿掉，模擬真的 API 的回傳
const DETAIL_ONLY = ["personCount", "invoiceType", "carrierNo", "lines"];
const toListItem = (order) =>
  Object.fromEntries(Object.entries(order).filter(([key]) => !DETAIL_ONLY.includes(key)));

/**
 * 取得我的訂單（篩選＋分頁）：[GET] /api/orders
 * 後端做好後換成：
 *   const { data } = await http.get("/orders", { params });
 * @param {object} params
 * @param {string} params.keyword 訂單編號「包含」，或場地名稱「包含」
 * @param {string|null} params.date 使用日期 "2026-10-20"；null 表示不篩選
 * @param {number|"all"} params.paymentMethod 付款方式；"all" 表示不篩選
 * @param {number|"all"} params.paymentStatus 付款狀態；"all" 表示不篩選
 * @param {number} params.page 第幾頁（從 1 開始）
 * @param {number} params.pageSize 每頁幾筆
 * @returns {Promise<{ items: object[], totalCount: number, page: number, pageSize: number }>}
 *   items：orderId、orderNo、reservationId、venueName、sportName、bookingDate、startTime、endTime、
 *   totalAmount、paymentMethod、paymentStatus、paymentDueAt、paidAt、reservationStatus、orderCreatedAt、
 *   paymentStartDeadline（最晚可以開始付款的時間；只有線上、未付款或付款失敗、預約待確認才有值）
 *   排序：下單時間新到舊；不含已作廢的預約
 */
export async function getMyOrders({
  keyword = "",
  date = null,
  paymentMethod = ALL,
  paymentStatus = ALL,
  page = 1,
  pageSize = 10,
} = {}) {
  await delay(300);
  const word = keyword.trim().toLowerCase();
  const filtered = myOrders
    .filter(
      (o) =>
        !word || o.orderNo.toLowerCase().includes(word) || o.venueName.toLowerCase().includes(word),
    )
    .filter((o) => !date || o.bookingDate === date)
    .filter((o) => paymentMethod === ALL || o.paymentMethod === paymentMethod)
    .filter((o) => paymentStatus === ALL || o.paymentStatus === paymentStatus)
    .sort((a, b) => b.orderCreatedAt.localeCompare(a.orderCreatedAt));

  const start = (page - 1) * pageSize;
  return {
    items: filtered.slice(start, start + pageSize).map(toListItem),
    totalCount: filtered.length,
    page,
    pageSize,
  };
}

/**
 * 取得一筆訂單的明細：[GET] /api/orders/{orderId}
 * 只能看自己的訂單：別人的訂單後端回 404（不回 403，避免讓人知道這個編號存在）
 * 後端做好後換成：
 *   const { data } = await http.get(`/orders/${orderId}`);
 * @param {number} orderId
 * @returns {Promise<object>} 列表的欄位＋personCount、invoiceType、carrierNo、
 *   lines: [{ slotTime, unitPrice, isPeak }]、
 *   payments: [{ paymentMethod, paymentChannel, paymentStatus, amount, paymentDueAt, paidAt, createdAt }]
 */
export async function getMyOrder(orderId) {
  await delay(300);
  const order = myOrders.find((o) => o.orderId === orderId);
  if (!order) {
    const error = new Error("找不到要求的資料");
    error.errorCode = "NotFound";
    throw error;
  }
  return {
    ...order,
    // 付款紀錄：目前一筆訂單一筆；3-C 綠界串接後，付款失敗再付一次會有多筆
    payments: [
      {
        paymentMethod: order.paymentMethod,
        paymentChannel: order.paymentMethod === 1 ? 1 : 0, // 線上：信用卡；現場：櫃台
        paymentStatus: order.paymentStatus,
        amount: order.totalAmount,
        paymentDueAt: order.paymentDueAt,
        paidAt: order.paidAt,
        createdAt: order.orderCreatedAt,
      },
    ],
  };
}
