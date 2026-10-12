// ────────────────────────────────────────────────────────────────
//  useSportTypes：運動類型清單（全站共用一份）
//
//  用法：
//    const { sportTypes, loading, error, findSportType } = useSportTypes();
//    // sportTypes：[{ sportTypeId, sportName, icon }]，icon 是完整的圖示名稱
//
//  為什麼不在每個元件各自呼叫 API：
//    同一頁可能有好幾個地方要用運動類型（例如預約頁的下拉選單和場地卡片），
//    狀態寫在函式外面，所有元件共用，整個網站只會向後端要一次。
//    讀取失敗時，下一次有元件呼叫 useSportTypes() 會再試一次。
// ────────────────────────────────────────────────────────────────
import { ref } from "vue";
import { getSportTypes } from "@/api/sportTypeApi";
import { getSportIcon } from "@/constants/sportTypes";

const sportTypes = ref([]);
const loading = ref(false);
const error = ref("");
// 讀取中的請求：同時有好幾個元件呼叫時，共用同一個，不會向後端要好幾次
// async 函式回傳的就是 Promise，所以可以直接存起來給大家 await
let request = null;

async function fetchSportTypes() {
  loading.value = true;
  error.value = "";
  try {
    const data = await getSportTypes();
    // 後端回傳 { id, name, icon }，在這裡轉成前端統一用的欄位名稱：
    // sportTypeId 跟網址參數、預約 API 的命名一致，元件都用這個名稱，只要在這裡轉一次
    sportTypes.value = data.map((sport) => ({
      sportTypeId: sport.id,
      sportName: sport.name,
      icon: getSportIcon(sport.icon),
    }));
  } catch (err) {
    error.value = err.message || "無法載入運動類型";
  } finally {
    loading.value = false;
    request = null;
  }
}

function load() {
  // 已經讀過了：不用再讀
  if (sportTypes.value.length > 0) return;
  // 還沒有人在讀才開始讀；已經在讀取中，就共用同一個請求（??= 只在 request 是 null 時才指定）
  request ??= fetchSportTypes();
  return request;
}

export function useSportTypes() {
  load();

  // 用 sportTypeId 找運動類型；網址參數是字串，所以用 Number() 比對
  const findSportType = (sportTypeId) =>
    sportTypes.value.find((sport) => sport.sportTypeId === Number(sportTypeId));

  return { sportTypes, loading, error, reload: load, findSportType };
}
