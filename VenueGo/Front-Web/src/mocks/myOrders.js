// 訂單與付款：切版用的假資料（串接 API 後就不再使用）
// 欄位格式照「將來 API 會回傳的格式」設計（3-A API 規格草稿），串接時頁面不用改
//
// 時間不寫死，用「現在往後幾分鐘」「今天往後幾天」在打開頁面時算出來：
// 寫死的話，過幾分鐘所有付款期限都過了，「付款」按鈕和「剩 N 分鐘」就測試不到
import { PAYMENT_METHOD } from "@/constants/payment";

// 期限前幾分鐘不能開始付款（之後由後端 ReservationRulesOptions 決定，後端回傳 paymentStartDeadline）
const START_CUTOFF_MINUTES = 5;
// 17:00 起是尖峰（假資料用；真的資料由後端依計價規則判斷）
const PEAK_FROM_HOUR = 17;

const pad = (n) => String(n).padStart(2, "0");
// Date → "2026-10-20T17:00:00"（跟 C# DateTime 的 JSON 一樣，不帶時區）
const toApiDateTime = (d) =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;
const toApiDate = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const minutesFromNow = (minutes) => new Date(Date.now() + minutes * 60000);
const daysFromToday = (days) => {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return d;
};

// 狀態數字（跟 constants/statuses.js、後端 enum 一致）
// 預約：0 待確認、1 已確認、2 已取消、3 已逾期
// 付款：0 未付款、1 付款處理中、2 已付款、3 付款失敗、4 付款取消、5 退款處理中
const { ONLINE, ONSITE } = PAYMENT_METHOD;

/**
 * 建立一筆訂單
 * @param {object} o
 * @param {number} o.days 使用日期距離今天幾天
 * @param {number|null} o.dueInMinutes 付款期限距離現在幾分鐘（線上付款用；現場付款用使用前 1 天）
 * @param {number} o.createdMinutesAgo 幾分鐘前下單（排序用）
 */
function order({
  orderId,
  reservationId,
  venueName,
  sportName,
  days,
  startTime,
  endTime,
  personCount,
  unitPrices, // 每一格的單價，依時間順序
  paymentMethod,
  paymentStatus,
  reservationStatus,
  dueInMinutes = null,
  paidMinutesAgo = null,
  createdMinutesAgo,
  invoiceType = 0,
  carrierNo = null,
}) {
  const bookingDay = daysFromToday(days);
  const bookingDate = toApiDate(bookingDay);
  const [startHour] = startTime.split(":").map(Number);
  const slotStart = new Date(bookingDay);
  slotStart.setHours(startHour, 0, 0, 0);

  const paymentDueAt =
    paymentMethod === ONLINE
      ? toApiDateTime(dueInMinutes != null ? minutesFromNow(dueInMinutes) : minutesFromNow(-60))
      : toApiDateTime(new Date(slotStart.getTime() - 86400000)); // 現場：使用前 1 天

  // 最晚可以開始付款的時間：只有「線上、未付款或付款失敗、預約待確認」才有值（後端計算）
  const payable =
    paymentMethod === ONLINE && [0, 3].includes(paymentStatus) && reservationStatus === 0;
  const paymentStartDeadline = payable
    ? toApiDateTime(new Date(new Date(paymentDueAt).getTime() - START_CUTOFF_MINUTES * 60000))
    : null;

  const createdAt = minutesFromNow(-createdMinutesAgo);
  const lines = unitPrices.map((unitPrice, i) => ({
    slotTime: `${pad(startHour + i)}:00`,
    unitPrice,
    isPeak: startHour + i >= PEAK_FROM_HOUR,
  }));

  return {
    // ── 列表的欄位 ──
    orderId,
    orderNo: `VG${toApiDate(createdAt).replaceAll("-", "")}${pad(orderId).padStart(3, "0")}`,
    reservationId,
    venueName,
    sportName,
    bookingDate,
    startTime,
    endTime,
    totalAmount: unitPrices.reduce((sum, p) => sum + p, 0),
    paymentMethod,
    paymentStatus,
    paymentDueAt,
    paidAt: paidMinutesAgo != null ? toApiDateTime(minutesFromNow(-paidMinutesAgo)) : null,
    reservationStatus,
    orderCreatedAt: toApiDateTime(createdAt),
    paymentStartDeadline,
    // ── 明細才有的欄位（getMyOrder 才回傳）──
    personCount,
    invoiceType,
    carrierNo,
    lines,
  };
}

// 每一筆都對應一種要測試的情況
// prettier-ignore
export const myOrders = [
  // 線上、未付款、還有 25 分鐘 → 有「付款」按鈕，剩 20 分鐘（期限前 5 分鐘截止）
  order({ orderId: 21, reservationId: 21, venueName: "籃球場-A", sportName: "籃球", days: 5, startTime: "20:00", endTime: "22:00", personCount: 4, unitPrices: [1500, 1500], paymentMethod: ONLINE, paymentStatus: 0, reservationStatus: 0, dueInMinutes: 25, createdMinutesAgo: 5, invoiceType: 1, carrierNo: "/AB12345" }),
  // 線上、付款失敗、還有 40 分鐘 → 有「付款」按鈕（可以重新付款）
  order({ orderId: 20, reservationId: 20, venueName: "羽球場-B", sportName: "羽球", days: 3, startTime: "10:00", endTime: "11:00", personCount: 2, unitPrices: [400], paymentMethod: ONLINE, paymentStatus: 3, reservationStatus: 0, dueInMinutes: 40, createdMinutesAgo: 8 }),
  // 線上、未付款、只剩 3 分鐘 → 已超過「最晚可以開始付款」，沒有按鈕，只顯示期限
  order({ orderId: 19, reservationId: 19, venueName: "桌球桌-02", sportName: "桌球", days: 1, startTime: "19:00", endTime: "20:00", personCount: 2, unitPrices: [300], paymentMethod: ONLINE, paymentStatus: 0, reservationStatus: 0, dueInMinutes: 3, createdMinutesAgo: 27 }),
  // 線上、付款處理中（客人在綠界付款頁）→ 沒有按鈕
  order({ orderId: 18, reservationId: 18, venueName: "匹克球場-A", sportName: "匹克球", days: 2, startTime: "15:00", endTime: "17:00", personCount: 4, unitPrices: [700, 900], paymentMethod: ONLINE, paymentStatus: 1, reservationStatus: 0, dueInMinutes: 18, createdMinutesAgo: 12 }),
  // 現場、未付款 → 沒有按鈕，期限是使用前 1 天（到櫃台付）
  order({ orderId: 17, reservationId: 17, venueName: "籃球場-B", sportName: "籃球", days: 6, startTime: "17:00", endTime: "19:00", personCount: 8, unitPrices: [1500, 1500], paymentMethod: ONSITE, paymentStatus: 0, reservationStatus: 0, createdMinutesAgo: 60 * 3 }),
  // 線上、已付款
  order({ orderId: 16, reservationId: 16, venueName: "羽球場-A", sportName: "羽球", days: 4, startTime: "18:00", endTime: "20:00", personCount: 4, unitPrices: [500, 500], paymentMethod: ONLINE, paymentStatus: 2, reservationStatus: 1, paidMinutesAgo: 60 * 20, createdMinutesAgo: 60 * 20 + 6, invoiceType: 1, carrierNo: "/AB12345" }),
  // 線上、沒付款 → 逾期（付款取消、預約已逾期）
  order({ orderId: 15, reservationId: 15, venueName: "網球場-A", sportName: "網球", days: 2, startTime: "09:00", endTime: "10:00", personCount: 2, unitPrices: [2500], paymentMethod: ONLINE, paymentStatus: 4, reservationStatus: 3, createdMinutesAgo: 60 * 26 }),
  // 線上、已付款後取消 → 退款處理中
  order({ orderId: 14, reservationId: 14, venueName: "桌球桌-01", sportName: "桌球", days: 8, startTime: "14:00", endTime: "15:00", personCount: 2, unitPrices: [300], paymentMethod: ONLINE, paymentStatus: 5, reservationStatus: 2, paidMinutesAgo: 60 * 40, createdMinutesAgo: 60 * 40 + 4 }),
  // 現場、已付款（櫃台收款）
  order({ orderId: 13, reservationId: 13, venueName: "籃球場-A", sportName: "籃球", days: -1, startTime: "19:00", endTime: "21:00", personCount: 10, unitPrices: [1500, 1500], paymentMethod: ONSITE, paymentStatus: 2, reservationStatus: 1, paidMinutesAgo: 60 * 30, createdMinutesAgo: 60 * 24 * 5 }),
  // ── 以下是比較舊的已付款訂單：測試分頁（每頁 10 筆）──
  order({ orderId: 12, reservationId: 12, venueName: "羽球場-A", sportName: "羽球", days: -3, startTime: "10:00", endTime: "12:00", personCount: 4, unitPrices: [400, 400], paymentMethod: ONLINE, paymentStatus: 2, reservationStatus: 1, paidMinutesAgo: 60 * 24 * 6, createdMinutesAgo: 60 * 24 * 6 + 3 }),
  order({ orderId: 11, reservationId: 11, venueName: "匹克球場-B", sportName: "匹克球", days: -5, startTime: "16:00", endTime: "17:00", personCount: 4, unitPrices: [700], paymentMethod: ONLINE, paymentStatus: 2, reservationStatus: 1, paidMinutesAgo: 60 * 24 * 8, createdMinutesAgo: 60 * 24 * 8 + 2 }),
  order({ orderId: 10, reservationId: 10, venueName: "桌球桌-03", sportName: "桌球", days: -7, startTime: "20:00", endTime: "21:00", personCount: 2, unitPrices: [400], paymentMethod: ONSITE, paymentStatus: 2, reservationStatus: 1, paidMinutesAgo: 60 * 24 * 7, createdMinutesAgo: 60 * 24 * 10 }),
];
