import http from "./http";
import { venuesMock } from "./mocks/venuesMock";

// 場館資訊頁：GET /api/venues/introduction
// 後端回傳 ApiResult，http.js 會自動取出 data → { businessHours: [...], sportTypes: [...] }
export async function getVenueIntroduction() {
  const { data } = await http.get("/venues/introduction");
  return data;
}

// 首頁場地介紹圖卡：GET /api/venues
// 【暫時】回傳假資料；後端完成後整段改成：
//   const { data } = await http.get("/venues");
//   return data;
export async function getVenues() {
  // 模擬網路延遲 0.5 秒，才看得到載入中的畫面
  await new Promise((resolve) => setTimeout(resolve, 500));
  return venuesMock.data;
}
