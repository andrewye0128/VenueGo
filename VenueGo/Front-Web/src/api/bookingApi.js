// 場地預約 API
// ⚠️ 後端 API 還沒做，目前用假資料（mocks/bookingAvailability.js）模擬
//    串接時只要把兩個函式的內容換成呼叫後端，頁面和元件都不用改
//    （網址是暫定的，要等後端 API 規格確定；改成呼叫 http 後，註解要照組內格式寫成「[方法] 網址」）
import { buildSlots, mockVenues } from "@/mocks/bookingAvailability";
import { SLOT_AVAILABILITY } from "@/constants/statuses";
import { isInPeriod } from "@/constants/booking";
import { delay } from "@/utils/delay";

const venuesOf = (sportTypeId) =>
  sportTypeId == null ? mockVenues : mockVenues.filter((v) => v.sportTypeId === sportTypeId);

/**
 * 某天各場地的時段（預計串接 /api/booking/availability）
 * @param {object} params
 * @param {number|null} params.sportTypeId 運動類型；null 表示全部運動
 * @param {string} params.date "2026-10-14"
 * @returns {Promise<{ date: string, venues: object[] }>}
 *   venues：[{ venueId, sportTypeId, venueName, capacity, photoPath, slots: [{ slotTime, availability, unitPrice, isPeak }] }]
 *   前端再依 sportTypeId 分組顯示
 */
export async function getAvailability({ sportTypeId = null, date }) {
  await delay(300); // 模擬網路等待
  const now = new Date();
  return {
    date,
    venues: venuesOf(sportTypeId).map((venue) => ({
      ...venue,
      slots: buildSlots(venue, date, now),
    })),
  };
}

/**
 * 一段日期內每天還剩幾格（日期列用，預計串接 /api/booking/availability/summary）
 * 後端已經有 ITimeSlotService 的「日期範圍統計」可以用
 * @param {object} params
 * @param {number|null} params.sportTypeId 運動類型；null 表示全部運動
 * @param {string[]} params.dates 要統計的日期 ["2026-10-14", ...]
 * @param {string} params.period 時段篩選（constants/booking.js 的 TIME_PERIODS key）
 * @returns {Promise<{ date: string, availableCount: number }[]>}
 */
export async function getAvailabilitySummary({ sportTypeId = null, dates, period = "all" }) {
  await delay(200); // 模擬網路等待
  const now = new Date();
  const venues = venuesOf(sportTypeId);
  return dates.map((date) => ({
    date,
    availableCount: venues
      .flatMap((venue) => buildSlots(venue, date, now))
      .filter(
        (s) => s.availability === SLOT_AVAILABILITY.AVAILABLE && isInPeriod(s.slotTime, period),
      ).length,
  }));
}
