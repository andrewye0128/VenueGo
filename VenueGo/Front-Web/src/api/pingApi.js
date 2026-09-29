import http from "./http";

// 測試前後端是否接通：GET /api/ping
// 後端回傳 ApiResult，http.js 會自動取出 data → { reply: "pong", time: "..." }
export async function getPing() {
  const { data } = await http.get("/ping");
  return data;
}
