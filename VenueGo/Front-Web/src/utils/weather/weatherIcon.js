// 中央氣象署的天氣文字 → 圖示（10/18，TopBar 即時氣象用）
//
// 用文字判斷、不用天氣代碼：代碼在觀測和預報兩份資料裡不一定有，文字一定有，
// 而且「多雲時晴」「陰短暫雨」這種組合，看關鍵字比背 42 個代碼可靠。
// 判斷順序就是重要性：有雷就畫雷，有雨就畫雨，最後才看雲和晴。

/**
 * @param {string | null | undefined} text 氣象署的天氣文字，例如「多雲時晴」
 * @param {boolean} night 晚上（18:00～06:00）用月亮代替太陽
 * @returns {string | null} Nuxt UI 的圖示名稱；沒有文字時回 null（不畫圖示）
 */
export function weatherIcon(text, night) {
  if (!text) return null;
  if (text.includes("雷")) return "i-lucide-cloud-lightning";
  if (text.includes("雪")) return "i-lucide-snowflake";
  if (text.includes("雨")) return "i-lucide-cloud-rain";
  if (text.includes("霧")) return "i-lucide-cloud-fog";
  if (text.includes("陰")) return "i-lucide-cloud";
  if (text.includes("雲") && text.includes("晴"))
    return night ? "i-lucide-cloud-moon" : "i-lucide-cloud-sun";
  if (text.includes("雲")) return "i-lucide-cloud";
  if (text.includes("晴")) return night ? "i-lucide-moon" : "i-lucide-sun";
  // 上面都沒對到：氣象署出現了沒看過的說法，先畫一朵雲，不要整個不見
  return "i-lucide-cloud";
}

/** 現在是不是晚上（18:00～06:00）。用瀏覽器的真實時間：天氣不跟時光機走。 */
export function isNightNow(date = new Date()) {
  const h = date.getHours();
  return h < 6 || h >= 18;
}
