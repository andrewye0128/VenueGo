// 我的預約 API
// 不用自己帶會員資訊：http.js 會自動在標頭加上登入的 JWT（Authorization: Bearer ...），
// 後端從 token 知道是哪位會員
import http from "./http";
import { ALL } from "@/constants/statuses";

/**
 * 取得我的預約（篩選＋分頁）：[GET] /api/reservations
 * @param {object} params
 * @param {string} params.keyword 比對場地名稱「包含」，或預約編號「完全相同」（R012、12 都找得到 R012）
 * @param {string|null} params.date 預約日期 "2026-10-08"；null 表示不篩選
 * @param {number|"all"} params.reservationStatus 預約狀態；"all" 表示不篩選
 * @param {number|"all"} params.paymentStatus 付款狀態；"all" 表示不篩選
 * @param {number} params.page 第幾頁（從 1 開始）
 * @param {number} params.pageSize 每頁幾筆（後端限制最多 50）
 * @returns {Promise<{ items: object[], totalCount: number, page: number, pageSize: number }>}
 */
export async function getMyReservations({
  keyword = "",
  date = null,
  reservationStatus = ALL,
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
  if (reservationStatus !== ALL) params.reservationStatus = reservationStatus;
  if (paymentStatus !== ALL) params.paymentStatus = paymentStatus;

  // http.js 已經把 ApiResult 的 data 取出來，這裡拿到的就是 { items, totalCount, page, pageSize }
  const { data } = await http.get("/reservations", { params });
  return data;
}

/**
 * 取消預約
 * ⚠️ 後端的取消 API 還沒做（預計是 POST 到 /api/reservations/{id}/cancel），先回傳「尚未開放」：
 *    取消視窗會把這個訊息顯示在視窗裡，按鈕和視窗都保留，API 做好後只要改這個函式
 * @param {number} reservationId
 * @param {string|null} reason 取消原因（選填，最多 200 字）
 */
export async function cancelMyReservation(reservationId, reason = null) {
  throw new Error("取消預約功能尚未開放，請洽櫃檯辦理");
}

/**
 * 建立預約：[POST] /api/reservations
 * 需要會員登入（http.js 自動帶 JWT）；預約記在登入的會員名下，不送會員
 * 不送金額：金額由後端依時段重新計算（送了也不會採用）
 * @param {object} request
 * @param {number} request.venueId
 * @param {string} request.bookingDate "2026-10-20"
 * @param {string[]} request.slotTimes ["17:00", "18:00"]
 * @param {number} request.personAmount 使用人數
 * @param {number} request.paymentMethod constants/payment.js 的 PAYMENT_METHOD
 * @param {number} request.invoiceType constants/payment.js 的 INVOICE_TYPE
 * @param {string|null} request.carrierNo 手機條碼載具；紙本發票時是 null
 * @param {boolean} request.termsAccepted 一定要是 true
 * @param {string} request.termsVersion 客人同意的條款版本（試算回傳的 termsVersion）
 * @returns {Promise<object>} { reservationId, orderNo, venueName, sportName, bookingDate, startTime, endTime,
 *   personAmount, totalAmount, paymentMethod, paymentDueAt, reservationStatus }
 *   成功是 201；錯誤：SlotTaken（409）、DateOutOfRange、VenueNotBookable、SlotUnavailable、
 *   PaymentMethodNotAllowed、TermsVersionMismatch、InvalidInput、ValidationFailed（400）、NotLoggedIn（401）
 */
export async function createReservation(request) {
  const { data } = await http.post("/reservations", request);
  return data;
}
