// 運動類型：切版用的假資料（後端 API 做好後就不再使用）
// 欄位對應資料表 SportTypes：SportTypeId、SportName、IconName
// sportTypeId、sportName 跟資料庫一致（2026-10-11 確認）
// ⚠️ 資料庫的 IconName 目前全部是 NULL，這裡的 iconName 是「建議填入的值」，資料庫填好後要再對一次
export const mockSportTypes = [
  { sportTypeId: 1, sportName: "羽球", iconName: "badminton" },
  { sportTypeId: 2, sportName: "籃球", iconName: "basketball" },
  { sportTypeId: 3, sportName: "網球", iconName: "tennis" },
  { sportTypeId: 4, sportName: "匹克球", iconName: "pickleball" },
  { sportTypeId: 5, sportName: "桌球", iconName: "table-tennis" },
];
