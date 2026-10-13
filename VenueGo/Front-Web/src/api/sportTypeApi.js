// 運動類型 API（後端：Controllers/Api/VenuesApi/SportTypeApiController.cs）
import http from "./http";

/**
 * 取得開放中的運動類型：[GET] /api/sport-types
 * 只回傳啟用中、而且至少有一個啟用中場地的運動，依 id 排序
 * @returns {Promise<{ id: number, name: string, icon: string, isBookable: boolean }[]>}
 *   icon 是完整的圖示名稱（例如 i-mdi-basketball）；資料庫沒設定時，後端回傳預設圖示，不會是 null
 *   isBookable：有啟用中的價格才是 true；false 時下拉選單顯示「（暫停開放）」、不能選
 *   元件請用 composables/useSportTypes.js，它會轉成 { sportTypeId, sportName, icon }
 */
export async function getSportTypes() {
  // http.js 已經把 ApiResult 的 data 取出來了
  const { data } = await http.get("/sport-types");
  return data;
}
