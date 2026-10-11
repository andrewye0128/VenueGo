// 運動類型 API
// ⚠️ 後端 API 還沒做，目前回傳假資料（mocks/sportTypes.js）
//    串接時把函式內容換成：const { data } = await http.get("/sport-types"); return data;
//    （網址是暫定的，要等後端 API 規格確定；改成呼叫 http 後，註解要照組內格式寫成「[方法] 網址」）
import { mockSportTypes } from "@/mocks/sportTypes";
import { delay } from "@/utils/delay";

/**
 * 取得開放中的運動類型
 * @returns {Promise<{ sportTypeId: number, sportName: string, iconName: string | null }[]>}
 */
export async function getSportTypes() {
  await delay(200); // 模擬網路等待
  return mockSportTypes.map((sport) => ({ ...sport }));
}
