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

// 時段 → "18:00-20:00"
// API 的時間可能是 "18:00" 或 "18:00:00"（C# TimeOnly 預設格式），只取到分鐘
export function formatTimeRange(startTime, endTime) {
  const hhmm = (time) => (time ?? "").slice(0, 5);
  return `${hhmm(startTime)}-${hhmm(endTime)}`;
}
