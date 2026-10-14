// 場地預約階段 2：試算、建立預約的假資料（後端 API 做好後就不再使用）
// 欄位照「將來 API 會回傳的格式」設計，也就是 2-2 API 規格的草稿
//
// 試算用的是「真的」時段資料（/api/booking/availability 已經做好了），
// 只有「能不能訂、付款方式、付款期限」這些規則先在這裡模擬，之後由後端計算
import { ErrorCodes } from "@/constants/errorCodes";
import { SLOT_AVAILABILITY } from "@/constants/statuses";
import { PAYMENT_METHOD } from "@/constants/payment";
import { MAX_SLOTS_PER_RESERVATION } from "@/constants/reservationRules";
import { slotEndTime } from "@/utils/slotSelection";

// 付款規則（之後在後端 ReservationRulesOptions）
const ONSITE_MIN_DAYS_AHEAD = 3; // 距離使用日至少 3 天才能選現場付款
const ONSITE_DUE_DAYS_BEFORE_USE = 1; // 現場付款：使用時段開始前 1 天
const ONLINE_DUE_MINUTES = 30; // 線上付款：建立後 30 分鐘（但不晚於時段開始）
const TERMS_VERSION = "v1.0"; // 跟後端 appsettings.json 的 ReservationRules.TermsVersion 一致

// 假資料用的錯誤：格式跟 http.js 整理後的錯誤一樣（message＋errorCode）
function apiError(message, errorCode) {
  const error = new Error(message);
  error.errorCode = errorCode;
  return error;
}

const pad = (n) => String(n).padStart(2, "0");
// Date → "2026-10-20T17:00:00"（跟 C# DateTime 的 JSON 一樣，不帶時區）
const toApiDateTime = (d) =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;
// "2026-10-20" + "17:00" → Date（當地時間）
const toDate = (date, time) => {
  const [y, m, d] = date.split("-").map(Number);
  const [hh, mm] = time.split(":").map(Number);
  return new Date(y, m - 1, d, hh, mm);
};

/**
 * 付款方式與期限（之後由後端計算）
 * 假資料用瀏覽器時間；後端會用網站時間（ITimeService，開發時跟著時光機）
 */
function buildPaymentOptions(bookingDate, startTime, now = new Date()) {
  const start = toDate(bookingDate, startTime);
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const daysAhead = Math.round((toDate(bookingDate, "00:00") - today) / 86400000);

  // 線上：建立＋30 分鐘，但如果時段更早開始，最晚到時段開始時間
  const onlineDue = new Date(Math.min(now.getTime() + ONLINE_DUE_MINUTES * 60000, start.getTime()));
  // 現場：使用時段開始前 1 天（10/20 17:00 → 10/19 17:00）
  const onsiteDue = new Date(start.getTime() - ONSITE_DUE_DAYS_BEFORE_USE * 86400000);
  const onsiteAllowed = daysAhead >= ONSITE_MIN_DAYS_AHEAD;

  return [
    {
      paymentMethod: PAYMENT_METHOD.ONLINE,
      isAvailable: true,
      dueAt: toApiDateTime(onlineDue),
      unavailableReason: null,
    },
    {
      paymentMethod: PAYMENT_METHOD.ONSITE,
      isAvailable: onsiteAllowed,
      dueAt: onsiteAllowed ? toApiDateTime(onsiteDue) : null,
      unavailableReason: onsiteAllowed
        ? null
        : `距離使用日不到 ${ONSITE_MIN_DAYS_AHEAD} 天，只能線上付款`,
    },
  ];
}

/**
 * 試算（假資料）：從真的時段資料算出 [GET] /api/booking/quote 將來的回傳格式
 * @param {object} availability getAvailability() 的回傳（某天各場地的時段）
 * @param {object} sport 運動類型（{ sportTypeId, sportName }），找不到是 undefined
 */
export function buildMockQuote(availability, { venueId, slots }, sport) {
  const venue = availability.venues.find((v) => v.venueId === venueId);
  if (!venue || !venue.isBookable) {
    throw apiError("這個場地目前無法預約，請選擇其他場地", ErrorCodes.VenueNotBookable);
  }

  // 每一格都要「可預約而且有價格」，跟預約頁的時段按鈕同一條規則
  const picked = slots.map((time) => venue.slots.find((s) => s.slotTime === time));
  const allOpen = picked.every(
    (s) => s && s.availability === SLOT_AVAILABILITY.AVAILABLE && s.unitPrice != null,
  );
  // 連續：每一格的結束時間等於下一格的開始時間
  const consecutive = slots.every((time, i) => i === 0 || slotEndTime(slots[i - 1]) === time);
  if (!allOpen || !consecutive || slots.length > MAX_SLOTS_PER_RESERVATION) {
    throw apiError("您選擇的時段已無法預約，請重新選擇", ErrorCodes.SlotUnavailable);
  }

  const startTime = slots[0];
  const endTime = slotEndTime(slots[slots.length - 1]);
  return {
    venueId: venue.venueId,
    venueName: venue.venueName,
    sportTypeId: venue.sportTypeId,
    sportName: sport?.sportName ?? "",
    capacity: venue.capacity,
    bookingDate: availability.date,
    startTime,
    endTime,
    slots: picked.map(({ slotTime, unitPrice, isPeak }) => ({ slotTime, unitPrice, isPeak })),
    totalAmount: picked.reduce((sum, s) => sum + s.unitPrice, 0),
    paymentOptions: buildPaymentOptions(availability.date, startTime),
    termsVersion: TERMS_VERSION,
  };
}

/**
 * 建立預約（假資料）：[POST] /api/reservations 將來的回傳格式
 * 只做最基本的檢查；真正的檢查（時段、金額、付款規則）在後端
 * @param {object} quote 填寫頁拿到的試算結果（假資料才需要，真的 API 由後端重新計算）
 * @param {object} request 送出的資料
 */
export function buildMockReservation(quote, request) {
  if (request.termsVersion !== TERMS_VERSION) {
    throw apiError("租借條款已更新，請重新閱讀並同意", ErrorCodes.TermsVersionMismatch);
  }
  const option = quote.paymentOptions.find((o) => o.paymentMethod === request.paymentMethod);
  if (!option?.isAvailable) {
    throw apiError(
      option?.unavailableReason ?? "付款方式不正確",
      ErrorCodes.PaymentMethodNotAllowed,
    );
  }

  const now = new Date();
  const datePart = `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}`;
  return {
    reservationId: 900 + Math.floor(Math.random() * 99),
    // 跟後端 OrderNoGenerator 一樣的格式：VG＋日期＋3 碼流水號
    orderNo: `VG${datePart}${String(Math.floor(Math.random() * 999) + 1).padStart(3, "0")}`,
    venueName: quote.venueName,
    sportName: quote.sportName,
    bookingDate: quote.bookingDate,
    startTime: quote.startTime,
    endTime: quote.endTime,
    personAmount: request.personAmount,
    totalAmount: quote.totalAmount,
    paymentMethod: request.paymentMethod,
    // 付款期限用「按下確認的這一刻」重新算（線上付款的 30 分鐘從建立時開始算）
    paymentDueAt: buildPaymentOptions(quote.bookingDate, quote.startTime, now).find(
      (o) => o.paymentMethod === request.paymentMethod,
    ).dueAt,
    reservationStatus: 0, // 待確認
  };
}
