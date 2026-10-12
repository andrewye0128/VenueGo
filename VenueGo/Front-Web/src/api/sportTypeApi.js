import http from "./http";

// 運動類型回傳
// 後端回傳 ApiResult，http.js 會自動取出 data
export async function getSportTypes() {
  const { data } = await http.get("/sport-types");
  return data;
}
