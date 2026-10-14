// 場地預約 API（前台預約頁用；查時段不需要登入，試算需要會員登入）
import http from "./http";
import { getSportTypes } from "./sportTypeApi";
import { DEFAULT_TIME_PERIOD } from "@/constants/booking";
import { buildMockQuote } from "@/mocks/bookingQuote";

/**
 * 某天各場地的時段：[GET] /api/booking/availability
 * @param {object} params
 * @param {number|null} params.sportTypeId 運動類型；null 表示全部運動（不送參數）
 * @param {string} params.date "2026-10-14"，必須在今天～30 天後，否則後端回 400 DateOutOfRange
 * @returns {Promise<{ date: string, venues: object[] }>}
 *   venues：[{ venueId, sportTypeId, venueName, capacity, photoPath, isBookable, slots: [{ slotTime, availability, unitPrice, isPeak }] }]
 *   slotTime 是 "18:00"；capacity、photoPath、unitPrice 可能是 null；公休日時 slots 是空陣列
 *   isBookable 是 false：場地暫停開放預約（目前是運動沒有啟用中的價格），日期列的剩餘格數也不會算它
 */
export async function getAvailability({ sportTypeId = null, date }) {
  const params = { date };
  // 全部運動就不送 sportTypeId：後端看到「沒有這個參數」就回傳所有運動
  if (sportTypeId != null) params.sportTypeId = sportTypeId;

  const { data } = await http.get("/booking/availability", { params });
  return data;
}

/**
 * 一段日期內每天還剩幾格（日期列用）：[GET] /api/booking/availability/summary
 * @param {object} params
 * @param {number|null} params.sportTypeId 運動類型；null 表示全部運動（不送參數）
 * @param {string[]} params.dates 日期列顯示的日期 ["2026-10-14", ...]（已排序），送出時取第一天和最後一天
 * @param {string} params.period 時段篩選（constants/booking.js 的 TIME_PERIODS key）；"all" 不送參數
 * @returns {Promise<{ date: string, availableCount: number }[]>} 範圍內的每一天都會出現
 */
export async function getAvailabilitySummary({
  sportTypeId = null,
  dates,
  period = DEFAULT_TIME_PERIOD,
}) {
  if (!dates?.length) return [];

  // 後端要的是範圍（from～to），不是每一天：日期列的日期是連續的，取頭尾就好
  const params = { from: dates[0], to: dates[dates.length - 1] };
  if (sportTypeId != null) params.sportTypeId = sportTypeId;
  if (period !== DEFAULT_TIME_PERIOD) params.period = period;

  const { data } = await http.get("/booking/availability/summary", { params });
  return data;
}

/**
 * 試算（填寫資料頁一打開就呼叫）：[GET] /api/booking/quote
 * 後端重新檢查時段還能不能訂、計算金額、回傳能用的付款方式與期限
 * ⚠️ 後端還沒做：先用真的時段資料（/api/booking/availability）在前端模擬，後端做好後換成：
 *    const { data } = await http.get("/booking/quote", { params: { venueId, date, slots: slots.join(",") } });
 * @param {object} params
 * @param {number} params.venueId 場地
 * @param {string} params.date "2026-10-20"
 * @param {string[]} params.slots 時段 ["17:00", "18:00"]（已排序）
 * @returns {Promise<object>} { venueId, venueName, sportTypeId, sportName, capacity, bookingDate, startTime, endTime,
 *   slots: [{ slotTime, unitPrice, isPeak }], totalAmount,
 *   paymentOptions: [{ paymentMethod, isAvailable, dueAt, unavailableReason }], termsVersion }
 *   錯誤：DateOutOfRange、SlotUnavailable、VenueNotBookable
 */
export async function getBookingQuote({ venueId, date, slots }) {
  // 兩個請求互不相關，同時送出（前端的 Promise.all 沒問題；後端同一個 DbContext 才不能同時查）
  const [availability, sportTypes] = await Promise.all([
    getAvailability({ date }),
    getSportTypes(),
  ]);
  const venue = availability.venues.find((v) => v.venueId === venueId);
  const sport = sportTypes.find((s) => s.id === venue?.sportTypeId);
  return buildMockQuote(availability, { venueId, slots }, sport && { sportName: sport.name });
}
