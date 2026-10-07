// 評論子系統的常數（昱）
// 這裡只放畫面上的固定文字與圖示。業務規則（能不能匿名、要不要公開）由後端決定，不放這裡。

// 兩種評論在「畫面上」的差別。
// 要查某一種評論的資料，請用 utils/review/reviewKinds.js 的 kindInfo()：不認得的種類會直接丟錯誤。
// 圖示名稱要寫完整字串：vite.config.js 已經把 src 裡的 .js 納入圖示掃描，寫在這裡的圖示也會被打包。
export const REVIEW_KINDS = Object.freeze({
  visit: {
    icon: "i-lucide-map-pin",
    subtitle: "這次到場使用的感想",
    placeholder: "",
  },
  booking: {
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
