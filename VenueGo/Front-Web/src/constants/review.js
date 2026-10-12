// 評論子系統的常數（昱）
// 這裡只放畫面上的固定文字與圖示。業務規則（能不能匿名、要不要公開）由後端決定，不放這裡。

// 兩種評論在「畫面上」的差別。
// 要查某一種評論的資料，請用 utils/review/reviewKinds.js 的 kindInfo()：不認得的種類會直接丟錯誤。
// 圖示名稱要寫完整字串：vite.config.js 已經把 src 裡的 .js 納入圖示掃描，寫在這裡的圖示也會被打包。
export const REVIEW_KINDS = Object.freeze({
  visit: {
    label: "現場評論", // 10/10：「我的評論」清單的卡片標題
    icon: "i-lucide-map-pin",
    subtitle: "這次到場使用的感想",
    placeholder: "",
  },
  booking: {
    label: "預約評論",
    icon: "i-lucide-receipt",
    subtitle: "這次預約流程的感想",
    placeholder: "訂位流程、付款方式、通知是否清楚，都可以暢所欲言",
  },
});

// 顧客對館方回覆的滿意度。value 對應後端 ReviewMain.ReplySatisfaction（0 不滿意／1 普通／2 滿意）。
// 顯示用的翻譯放在前端：它純粹是畫面文字，不影響任何資料。陣列順序就是畫面上按鈕的順序。
export const REPLY_SATISFACTION = Object.freeze([
  { value: 2, text: "滿意", icon: "i-lucide-smile" },
  { value: 1, text: "普通", icon: "i-lucide-meh" },
  { value: 0, text: "不滿意", icon: "i-lucide-frown" },
]);

// 「我的評論」清單上的標籤（10/10）。格式跟 constants/statuses.js 一樣：{ label, color, icon }，
// 直接丟給 components/list/StatusBadge.vue 顯示。
// 顏色照設計規範：warning 快到期、primary 館方回覆、neutral 一般資訊、success 公開中、error 已下架。
// 圖示名稱要寫完整字串，Nuxt UI 打包時才掃描得到。
export const MY_REVIEW_BADGES = Object.freeze({
  expiringSoon: { label: "快到期", color: "warning", icon: "i-lucide-alarm-clock" },
  hasDraft: { label: "有草稿", color: "neutral", icon: "i-lucide-file-pen-line" },
  newReply: { label: "館方新回覆", color: "primary", icon: "i-lucide-message-circle-more" },
  replied: { label: "館方已回覆", color: "primary", icon: "i-lucide-reply" },
  noReply: { label: "尚未回覆", color: "neutral", icon: "i-lucide-hourglass" },
  public: { label: "公開中", color: "success", icon: "i-lucide-globe" },
  private: { label: "不公開", color: "neutral", icon: "i-lucide-lock" },
  spam: { label: "已下架", color: "error", icon: "i-lucide-octagon-alert" },
});
