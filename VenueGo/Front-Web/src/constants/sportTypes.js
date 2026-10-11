// 運動類型的圖示對照表
// 運動類型清單本身由後端提供（api/sportTypeApi.js，元件裡用 composables/useSportTypes.js）
//
// 為什麼要對照表：資料庫 SportTypes.IconName 存的是短名稱（例如 basketball），
// 但 Nuxt UI 打包圖示時是「掃描程式碼裡寫完整的圖示名稱」，
// 用 `i-mdi-${iconName}` 組出來的名稱掃描不到，圖示會變成空白（或執行時才向網路下載）。
// 所以每個 IconName 都要在這裡寫一次完整名稱。資料庫新增運動時，這裡也要補一筆。
export const SPORT_ICONS = {
  basketball: "i-mdi-basketball",
  badminton: "i-mdi-badminton",
  "table-tennis": "i-mdi-table-tennis",
  tennis: "i-mdi-tennis",
  volleyball: "i-mdi-volleyball",
  swimming: "i-mdi-swim",
  // Material Design Icons 沒有匹克球，先用通用的運動圖示
  pickleball: "i-mdi-run",
};

// 對照表裡沒有的 IconName（或資料庫沒填）用這個
export const DEFAULT_SPORT_ICON = "i-mdi-run";

// IconName → 完整圖示名稱
export const getSportIcon = (iconName) => SPORT_ICONS[iconName] ?? DEFAULT_SPORT_ICON;

// 「全部運動」選項的值（搜尋、篩選用）
// 不能用空字串：Reka UI 規定空字串代表「還沒選」
// 送出搜尋條件前，請把 ALL_SPORTS 轉成「不篩選運動類型」
export const ALL_SPORTS = "all";
