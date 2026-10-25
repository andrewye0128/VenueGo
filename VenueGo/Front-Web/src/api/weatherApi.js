// 首頁 TopBar 即時氣象的 API（10/18）
// 前台不直接呼叫中央氣象署：授權碼只放在後端，後端也會快取 10 分鐘。
//
// 這裡的函式都不 catch，錯誤由呼叫的元件處理（TopBarWeather.vue：抓不到就不顯示天氣）。
import http from "./http";

// 現在天氣＋三天預報：[GET] /api/weather
// 回傳 { placeName, current: { temperature, weather, observedAt } | null, days: [{ date, label, min, max, weather }] }；
// （10/25：days 多了 rainChance，降雨機率 0～100，拿不到是 null）
// 後端沒有設定授權碼時回 null。
export async function getWeather() {
  const { data } = await http.get("/weather");
  return data;
}
