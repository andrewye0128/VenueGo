// 運動類型 API
import http from "./http";

/**
 * 取得開放中的運動類型：[GET] /api/sport-types
 * 不需要登入。只回傳啟用中的運動，依 sportTypeId 排序
 * @returns {Promise<{ sportTypeId: number, sportName: string, iconName: string | null }[]>}
 *   iconName 是圖示短名稱（例如 basketball），由 constants/sportTypes.js 轉成完整圖示名稱
 */
export async function getSportTypes() {
  // http.js 已經把 ApiResult 的 data 取出來
  const { data } = await http.get("/sport-types");
  return data;
}
