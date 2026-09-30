// 兩種評論在「畫面上」的差別，集中寫在這裡
//
// ⚠️ 每一種都明確列出來，不寫成「是 visit 就 A，否則就是 booking」。
//    哪天多了第三種評論，寫成「否則」的地方會默默把它當成 booking 顯示，而且不會有任何錯誤訊息。
//    這裡遇到不認得的種類會直接丟錯誤，第一時間就看得到。
//
// 業務規則（能不能匿名、要不要公開）不在這裡，那些由後端決定。這裡只放圖示、文字這類純顯示的東西。
// 圖示不放這裡，放在 ReviewKindIcon.vue：Nuxt UI 只掃描 .vue 檔找出用到哪些圖示再打包，
// 寫在 .js 裡的圖示名稱掃不到，執行時會改向網路要，連不上就是一片空白（9/29 實測）。
const KINDS = {
  visit: {
    subtitle: "這次到場使用的感想",
    placeholder: "",
  },
  booking: {
    subtitle: "這次預約流程的感想",
    placeholder: "訂位流程、付款方式、通知是否清楚，都可以暢所欲言",
  },
};

export function kindInfo(kind) {
  const info = KINDS[kind];
  if (!info) throw new Error(`[reviewKinds] 不認得的評論種類：${kind}`);
  return info;
}
