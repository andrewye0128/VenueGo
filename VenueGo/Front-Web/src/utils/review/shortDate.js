// 公開評論卡上的日期（10/15）：只顯示日期，不顯示幾點。
// 今年的寫「10/12」，不是今年的寫「2025/10/12」。
// 參數可以是 "2026-10-12"（DateOnly）或 "2026-10-12T18:05:00"（DateTime 的 ISO 字串），只取日期的部分。
// ⚠️ 不用 new Date(...) 解析：只有日期的字串會被當成 UTC 午夜，換成台灣時間雖然還是同一天，
//    但其他時區（例如美西）就會變成前一天。直接切字串最穩。

export function datePart(value) {
  return typeof value === "string" ? value.slice(0, 10) : null;
}

export function shortDate(value, thisYear = new Date().getFullYear()) {
  const part = datePart(value);
  if (!part) return "";
  const [y, m, d] = part.split("-").map(Number);
  return y === thisYear ? `${m}/${d}` : `${y}/${m}/${d}`;
}
