// 訂單與付款 API（會員中心「訂單與付款」頁）
// 不用自己帶會員資訊：http.js 會自動在標頭加上登入的 JWT（Authorization: Bearer ...），
// 後端從 token 知道是哪位會員
import http from "./http";
import { ALL } from "@/constants/statuses";

/**
 * 取得我的訂單（篩選＋分頁）：[GET] /api/orders
 * @param {object} params
 * @param {string} params.keyword 訂單編號「包含」，或場地名稱「包含」
 * @param {string|null} params.date 使用日期 "2026-10-20"；null 表示不篩選
 * @param {number|"all"} params.paymentMethod 付款方式；"all" 表示不篩選
 * @param {number|"all"} params.paymentStatus 付款狀態；"all" 表示不篩選
 * @param {number} params.page 第幾頁（從 1 開始）
 * @param {number} params.pageSize 每頁幾筆（後端限制最多 50）
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
  // 沒有值、選「全部」的條件不送：後端看到「沒有這個參數」就不篩選
  // （"all" 只是前端下拉選單用的值，後端不認得）
  const params = { page, pageSize };
  const word = keyword.trim();
  if (word) params.keyword = word;
  if (date) params.date = date;
  if (paymentMethod !== ALL) params.paymentMethod = paymentMethod;
  if (paymentStatus !== ALL) params.paymentStatus = paymentStatus;

  // http.js 已經把 ApiResult 的 data 取出來，這裡拿到的就是 { items, totalCount, page, pageSize }
  const { data } = await http.get("/orders", { params });
  return data;
}

/**
 * 取得一筆訂單的明細：[GET] /api/orders/{orderId}
 * 只能看自己的訂單：別人的訂單後端回 404（不回 403，避免讓人知道這個編號存在）
 * @param {number} orderId
 * @returns {Promise<object>} 列表的欄位＋personCount、invoiceType、carrierNo、
 *   lines: [{ slotTime, unitPrice, isPeak }]、
 *   payments: [{ paymentMethod, paymentChannel, paymentStatus, amount, paymentDueAt, paidAt, createdAt }]（新到舊）
 *   錯誤：NotFound（404，不存在、不是自己的、預約已作廢）、NotLoggedIn（401）
 */
export async function getMyOrder(orderId) {
  const { data } = await http.get(`/orders/${orderId}`);
  return data;
}

/**
 * 開始線上付款：[POST] /api/orders/{orderId}/payment/start
 * 後端檢查可以付款後，把付款改成「付款處理中」，並開出一張綠界付款單
 * 拿到的付款單交給 utils/ecpayRedirect.js 的 submitToEcpay，畫面就會跳到綠界刷卡頁
 * @param {number} orderId
 * @returns {Promise<{ actionUrl: string, fields: Record<string, string> }>}
 *   fields 的名稱是綠界規定的大小寫（MerchantID…），要原封不動送出，改一個字防偽章就對不上
 *   錯誤（error.errorCode）：InProgress、AlreadyPaid、NotPayable、PastStartDeadline、NotOnlinePayment、
 *   Conflict、InvalidAmount（409）；NotFound（404）；NotConfigured、TradeNoUnavailable（503）；NotLoggedIn（401）
 */
export async function startPayment(orderId) {
  const { data } = await http.post(`/orders/${orderId}/payment/start`);
  return data;
}
