// 畫面上顯示用的格式（日期補 0、時段）
// 只負責「給人看的文字」；送給 API 的日期請用 CalendarDate.toString()（2026-10-05）

const pad = (n) => String(n).padStart(2, "0");

// 日期 → "2026/09/30"
// 接受 CalendarDate（UCalendar、DatePickerInput 的值）或 API 回傳的 "2026-09-30" 字串
// 字串不用 new Date() 轉換：new Date("2026-09-30") 會當成國際標準時間，台灣以外的時區可能差一天
export function formatDate(date) {
  if (!date) return "";
  if (typeof date === "string") {
    const [year, month, day] = date.split("-");
    return `${year}/${pad(month)}/${pad(day)}`;
  }
  return `${date.year}/${pad(date.month)}/${pad(date.day)}`;
}

// 日期 → "2026/10/20（二）"
// 只接受 "2026-10-20" 字串；用年月日建立當地時間的 Date 來算星期，不會被時區影響
const WEEKDAYS = ["日", "一", "二", "三", "四", "五", "六"];
export function formatDateWithWeekday(date) {
  if (!date) return "";
  const [year, month, day] = date.split("-").map(Number);
  return `${formatDate(date)}（${WEEKDAYS[new Date(year, month - 1, day).getDay()]}）`;
}

// 日期時間 → "2026/10/07 14:30"
// 接受 API 回傳的 "2026-10-07T14:30:00"（C# DateTime 預設格式，沒有時區）
// 一樣直接拆字串，不用 new Date()，避免被換算時區
export function formatDateTime(dateTime) {
  if (!dateTime) return "";
  const [date, time = ""] = dateTime.split("T");
  return `${formatDate(date)} ${time.slice(0, 5)}`.trim();
}

// 金額 → "NT$ 1,200"
export function formatAmount(amount) {
  if (amount == null) return "";
  return `NT$ ${amount.toLocaleString("zh-TW")}`;
}

// 時段 → "18:00-20:00"
// API 的時間可能是 "18:00" 或 "18:00:00"（C# TimeOnly 預設格式），只取到分鐘
export function formatTimeRange(startTime, endTime) {
  const hhmm = (time) => (time ?? "").slice(0, 5);
  return `${hhmm(startTime)}-${hhmm(endTime)}`;
}
