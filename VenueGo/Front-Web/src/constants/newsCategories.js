// 最新消息分類（列表頁的分類頁籤使用）
// value 是後端存的分類代碼，label 是顯示的中文名稱，順序就是頁籤順序
// 要跟後端 Models/ContentModels/NewsCategoryOptions.cs 的 All 同步：增減分類、改名稱時兩邊都要改
export const newsCategories = [
  { value: "announcement", label: "場館公告" },
  { value: "venue", label: "場地異動" },
  { value: "event", label: "活動資訊" },
  { value: "promotion", label: "優惠資訊" },
  { value: "other", label: "其他" },
];

// 分類代碼是否在清單內：網址上的 ?category= 可能被亂改，不在清單內就當作「全部」
export function isNewsCategory(value) {
  return newsCategories.some((category) => category.value === value);
}
