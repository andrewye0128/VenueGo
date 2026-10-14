// 場地預約：切版用的假資料（後端 API 做好後就不再使用）
// 欄位照「將來 API 會回傳的格式」設計，對應後端 TimeSlotStatus（SlotTime、Availability、UnitPrice、IsPeak）
//
// 時段狀態不是亂數：用「場地＋日期＋時間」算出固定的結果，
// 重新整理頁面、切換日期再切回來，同一格的狀態都一樣，方便測試
import { SLOT_AVAILABILITY } from "@/constants/statuses";

// 營業時間 10:00～22:00，一格一小時（後端由 WeekBusinessHours 決定，這裡先寫死）
const OPEN_HOUR = 10;
const CLOSE_HOUR = 22;
// 18:00 起是尖峰
const PEAK_FROM_HOUR = 18;

// 場地（sportTypeId 跟資料庫一致：1 羽球、2 籃球、3 網球、4 匹克球、5 桌球）
// photoPath：大部分是 null（測試沒有照片時的圖示），一筆故意給不存在的圖片（測試讀取失敗）
export const mockVenues = [
  { venueId: 11, sportTypeId: 1, venueName: "羽球場-A", capacity: 4, photoPath: null },
  { venueId: 12, sportTypeId: 1, venueName: "羽球場-B", capacity: 4, photoPath: null },
  {
    venueId: 13,
    sportTypeId: 1,
    venueName: "羽球場-C",
    capacity: 4,
    photoPath: "/images/venues/not-exist.jpg",
  },
  { venueId: 21, sportTypeId: 2, venueName: "籃球場-A", capacity: 10, photoPath: null },
  { venueId: 22, sportTypeId: 2, venueName: "籃球場-B", capacity: 10, photoPath: null },
  { venueId: 31, sportTypeId: 3, venueName: "網球場-A", capacity: 4, photoPath: null },
  { venueId: 32, sportTypeId: 3, venueName: "網球場-B", capacity: 4, photoPath: null },
  { venueId: 41, sportTypeId: 4, venueName: "匹克球場-A", capacity: 4, photoPath: null },
  { venueId: 51, sportTypeId: 5, venueName: "桌球桌-01", capacity: 4, photoPath: null },
  { venueId: 52, sportTypeId: 5, venueName: "桌球桌-02", capacity: 4, photoPath: null },
  { venueId: 53, sportTypeId: 5, venueName: "桌球桌-03", capacity: 4, photoPath: null },
];

// 每種運動的單價：[離峰, 尖峰]
const PRICES = { 1: [300, 400], 2: [500, 600], 3: [400, 500], 4: [350, 450], 5: [150, 200] };

// 簡單的雜湊：同樣的輸入一定得到同樣的數字
function hash(text) {
  let h = 0;
  for (const ch of text) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return h;
}

const pad = (n) => String(n).padStart(2, "0");
const localDateString = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

// 每月 13 號公休（測試「這天沒有開放」）
const isClosedDay = (date) => date.endsWith("-13");

/**
 * 某場地某天的時段
 * @param {object} venue
 * @param {string} date "2026-10-14"
 * @param {Date} now 現在時間：今天已經開始的時段算「已過時間」（同後端 AllowCurrentHourSlot = false）
 */
export function buildSlots(venue, date, now) {
  if (isClosedDay(date)) return [];

  const [offPeak, peak] = PRICES[venue.sportTypeId];
  const isToday = date === localDateString(now);
  // 某些「場地＋日期」整天客滿（測試「已額滿」）
  const fullyBooked = hash(`${venue.venueId}${date}full`) % 9 === 0;

  const slots = [];
  for (let hour = OPEN_HOUR; hour < CLOSE_HOUR; hour++) {
    const roll = hash(`${venue.venueId}${date}${hour}`) % 20;
    let availability = SLOT_AVAILABILITY.AVAILABLE;
    if (isToday && hour <= now.getHours()) availability = SLOT_AVAILABILITY.PAST;
    else if (fullyBooked || roll < 6) availability = SLOT_AVAILABILITY.BOOKED;
    else if (roll === 19) availability = SLOT_AVAILABILITY.UNAVAILABLE;

    const isPeak = hour >= PEAK_FROM_HOUR;
    slots.push({
      slotTime: `${pad(hour)}:00`,
      availability,
      unitPrice: isPeak ? peak : offPeak,
      isPeak,
    });
  }
  return slots;
}
