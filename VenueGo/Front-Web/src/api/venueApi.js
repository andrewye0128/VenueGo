import http from "./http";

// 場館資訊頁：GET /api/venues/introduction
// 後端回傳 ApiResult，http.js 會自動取出 data
export async function getVenueIntroduction() {
  const { data } = await http.get("/venues/introduction");
  return data;
}

// 首頁場地介紹圖卡：GET /api/venues
export async function getVenues() {
  const { data } = await http.get("/venues");
  return data;
}
