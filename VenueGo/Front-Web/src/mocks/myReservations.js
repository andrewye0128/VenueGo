// 我的預約：切版用的假資料（串接 API 後就不再使用）
// 欄位格式照「將來 API 會回傳的格式」設計，串接時頁面不用改
//
// 日期不寫死，用「今天往前／往後幾天」在打開頁面時算出來：
// 寫死的話，過幾天所有預約都變成過去的，「取消」按鈕就測試不到了
import { Time, getLocalTimeZone, toCalendarDateTime, today } from "@internationalized/date";

const todayDate = today(getLocalTimeZone());

// 建立一筆預約
// days：預約日期距離今天幾天（+1 明天、-7 七天前、0 今天）
function reservation(
  reservationId,
  days,
  venueName,
  sportName,
  startTime,
  endTime,
  personCount,
  totalAmount,
  reservationStatus,
  paymentStatus,
  extra = {},
) {
  const bookingDate = todayDate.add({ days });
  const [hour, minute] = startTime.split(":").map(Number);
  const startAt = toCalendarDateTime(bookingDate, new Time(hour, minute));
  return {
    reservationId,
    venueName,
    sportName,
    bookingDate: bookingDate.toString(), // "2026-10-08"，跟 C# DateOnly 的輸出格式一樣
    startTime,
    endTime,
    personCount,
    totalAmount,
    reservationStatus,
    paymentStatus,
    // 付款期限：使用時段開始前 2 小時（假設值，實際規則由後端決定）
    paymentDueAt: startAt.subtract({ hours: 2 }).toString(), // "2026-10-08T16:00:00"
    // 建立時間：預約日期的 3 天前 14:30
    reservedAt: toCalendarDateTime(bookingDate.subtract({ days: 3 }), new Time(14, 30)).toString(),
    cancelledAt: null,
    cancelReason: null,
    ...extra,
  };
}

// 取消相關欄位：取消時間是預約日期的 1 天前 10:00
const cancelled = (days, cancelReason) => ({
  cancelledAt: toCalendarDateTime(todayDate.add({ days: days - 1 }), new Time(10, 0)).toString(),
  cancelReason,
});

// 狀態數字（跟 constants/statuses.js、後端 enum 一致）
// 預約：0 待確認、1 已確認、2 已取消、3 已逾期、4 已完成、6 場館取消、7 已作廢
// 付款：0 未付款、1 付款處理中、2 已付款、3 付款失敗、4 付款取消、5 退款處理中、6 已退款
// 參數順序：id、距今天數、場地、運動、開始、結束、人數、金額、預約狀態、付款狀態、取消資訊
// prettier-ignore：讓每筆維持一行、像表格一樣好對照（不然 Prettier 會把每筆拆成 12 行）
// prettier-ignore
export const myReservations = [
  // ── 設計稿上的 3 筆 ──
  reservation(1, 2, "羽球場 A", "羽球", "18:00", "20:00", 4, 800, 1, 2),
  reservation(2, 1, "第一籃球場", "籃球", "20:00", "21:00", 10, 1200, 0, 0),
  reservation(3, -5, "桌球室 3 號台", "桌球", "09:00", "10:00", 2, 200, 2, 6, cancelled(-5, "臨時有事無法前往")),

  // ── 今天：一筆晚上（還沒開始，可以取消）、一筆一大早（已經開始，不能取消） ──
  reservation(4, 0, "羽球場 B", "羽球", "21:00", "22:00", 2, 400, 1, 2),
  reservation(5, 0, "第二籃球場", "籃球", "07:00", "08:00", 6, 600, 1, 2),

  // ── 未來 ──
  reservation(6, 3, "匹克球場 1", "匹克球", "10:00", "12:00", 4, 900, 0, 1),
  reservation(7, 5, "桌球室 1 號台", "桌球", "19:00", "21:00", 2, 400, 1, 2),
  reservation(8, 7, "排球場", "排球", "14:00", "16:00", 12, 1600, 0, 0),
  reservation(9, 10, "羽球場 A", "羽球", "08:00", "10:00", 4, 700, 1, 2),
  reservation(10, 14, "第一籃球場", "籃球", "18:00", "20:00", 10, 2400, 0, 0),
  reservation(11, 20, "匹克球場 2", "匹克球", "16:00", "17:00", 4, 450, 2, 4, cancelled(15, null)),
  reservation(12, 29, "羽球場 C", "羽球", "20:00", "22:00", 4, 800, 6, 5, cancelled(20, "場館設備維修")),

  // ── 過去 ──
  reservation(13, -1, "羽球場 A", "羽球", "19:00", "21:00", 4, 800, 4, 2),
  reservation(14, -2, "第二籃球場", "籃球", "20:00", "22:00", 8, 2400, 4, 2),
  reservation(15, -3, "桌球室 2 號台", "桌球", "15:00", "16:00", 2, 200, 3, 4),
  reservation(16, -7, "匹克球場 1", "匹克球", "09:00", "11:00", 4, 900, 4, 2),
  reservation(17, -9, "羽球場 B", "羽球", "18:00", "19:00", 2, 400, 2, 5, cancelled(-9, "行程變更")),
  reservation(18, -12, "排球場", "排球", "13:00", "15:00", 12, 1600, 4, 2),
  reservation(19, -15, "第一籃球場", "籃球", "19:00", "21:00", 10, 2400, 7, 4),
  reservation(20, -18, "桌球室 3 號台", "桌球", "10:00", "12:00", 2, 400, 4, 2),
  reservation(21, -21, "羽球場 C", "羽球", "07:00", "09:00", 4, 700, 3, 4),
  reservation(22, -25, "匹克球場 2", "匹克球", "17:00", "18:00", 4, 450, 4, 2),
  reservation(23, -30, "羽球場 A", "羽球", "20:00", "22:00", 4, 800, 6, 6, cancelled(-31, "颱風停館")),
  reservation(24, -40, "第二籃球場", "籃球", "18:00", "19:00", 6, 600, 4, 2),
  reservation(25, -60, "桌球室 1 號台", "桌球", "14:00", "15:00", 2, 200, 4, 2),
];
