import http from "./http";

// 場館資訊頁：GET /api/venues/introduction
// 後端回傳 ApiResult，http.js 會自動取出 data → { businessHours: [...], sportTypes: [...] }
export async function getVenueIntroduction() {
  const { data } = await http.get("/venues/introduction");
  return data;
}
