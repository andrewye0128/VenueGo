// 運動類型的圖示清單
// 運動類型清單本身由後端提供（api/sportTypeApi.js，元件裡用 composables/useSportTypes.js）
//
// 為什麼要列出所有圖示：資料庫 SportTypes.IconName 存的是完整圖示名稱（例如 i-mdi-basketball），
// 由後台「運動類型管理 → 圖示設定」選擇。但 Nuxt UI 打包圖示時，是「掃描程式碼裡寫出來的圖示名稱」，
// 只存在資料庫、沒寫在程式碼裡的名稱掃描不到，畫面上的圖示會變成空白。
// 所以後台能選的圖示，都要在這裡寫一次。
//
// ⚠️ 要跟後端 Models/VenueModels/SportTypeIconOptions.cs 的 All 清單保持一致：
//    後台新增可選的圖示時，這裡也要補一筆，否則選了那個圖示的運動會顯示預設圖示
export const SPORT_ICON_NAMES = [
  // 球類
  "i-mdi-basketball",
  "i-mdi-badminton",
  "i-mdi-tennis",
  "i-mdi-table-tennis",
  "i-mdi-racquetball",
  "i-mdi-volleyball",
  "i-mdi-soccer",
  "i-mdi-baseball",
  "i-mdi-football",
  "i-mdi-rugby",
  "i-mdi-handball",
  "i-mdi-cricket",
  "i-mdi-hockey-sticks",
  "i-mdi-golf",
  "i-mdi-bowling",
  "i-mdi-billiards",
  // 水上
  "i-mdi-swim",
  "i-mdi-diving",
  // 健身、武術
  "i-mdi-dumbbell",
  "i-mdi-weight-lifter",
  "i-mdi-yoga",
  "i-mdi-gymnastics",
  "i-mdi-karate",
  "i-mdi-boxing-glove",
  "i-mdi-fencing",
  // 其他
  "i-mdi-run",
  "i-mdi-bike",
  "i-mdi-skateboarding",
  "i-mdi-ski",
  "i-mdi-dance-ballroom",
];

// 預設圖示：跟後端 SportTypeIconOptions.DefaultIcon 一樣
// 後端在資料庫沒設定圖示時就會回傳這個；前端遇到清單裡沒有的名稱也用這個
export const DEFAULT_SPORT_ICON = "i-mdi-help-circle-outline";

// 後端回傳的圖示名稱 → 畫面上要用的圖示
// 不在清單裡的名稱（例如後台新增了圖示、前端還沒補，或舊資料是短名稱 basketball）
// 打包時沒有這個圖示，直接用會是空白，所以改用預設圖示
export const getSportIcon = (icon) => (SPORT_ICON_NAMES.includes(icon) ? icon : DEFAULT_SPORT_ICON);

// 「全部運動」選項的值（搜尋、篩選用）
// 不能用空字串：Reka UI 規定空字串代表「還沒選」
// 送出搜尋條件前，請把 ALL_SPORTS 轉成「不篩選運動類型」
export const ALL_SPORTS = "all";
