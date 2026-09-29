// 運動類型清單（暫時寫死，之後改為從後端 API 取得）
// value 之後要對應後端 SportType 的資料；icon 使用 Iconify 名稱（Material Design Icons）
export const sportTypes = [
  { label: "籃球", value: "basketball", icon: "i-mdi-basketball" },
  { label: "羽球", value: "badminton", icon: "i-mdi-badminton" },
  { label: "桌球", value: "table-tennis", icon: "i-mdi-table-tennis" },
  { label: "排球", value: "volleyball", icon: "i-mdi-volleyball" },
  { label: "游泳", value: "swimming", icon: "i-mdi-swim" },
];

// 「全部運動」選項的值（搜尋、篩選用）
// 不能用空字串：Reka UI 規定空字串代表「還沒選」
// 送出搜尋條件前，請把 ALL_SPORTS 轉成「不篩選運動類型」
export const ALL_SPORTS = "all";
