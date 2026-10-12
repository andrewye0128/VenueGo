// 場地預約 API（前台預約頁用，都不需要登入）
import http from "./http";
import { DEFAULT_TIME_PERIOD } from "@/constants/booking";

/**
 * 某天各場地的時段：[GET] /api/booking/availability
 * @param {object} params
 * @param {number|null} params.sportTypeId 運動類型；null 表示全部運動（不送參數）
 * @param {string} params.date "2026-10-14"，必須在今天～30 天後，否則後端回 400 DateOutOfRange
 * @returns {Promise<{ date: string, venues: object[] }>}
 *   venues：[{ venueId, sportTypeId, venueName, capacity, photoPath, slots: [{ slotTime, availability, unitPrice, isPeak }] }]
 *   slotTime 是 "18:00"；capacity、photoPath、unitPrice 可能是 null；公休日時 slots 是空陣列
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
