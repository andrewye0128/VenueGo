// 評論種類的查詢（昱）
//
// ⚠️ 每一種評論都明確列在 constants/review.js 的 REVIEW_KINDS，不寫成「是 visit 就 A，否則就是 booking」。
//    哪天多了第三種評論，寫成「否則」的地方會默默把它當成 booking 顯示，而且不會有任何錯誤訊息。
//    這裡遇到不認得的種類會直接丟錯誤，第一時間就看得到。
import { REVIEW_KINDS } from "@/constants/review";

/** 取得某一種評論的圖示與文字：{ icon, subtitle, placeholder } */
export function kindInfo(kind) {
  // 用 hasOwn，不直接用 REVIEW_KINDS[kind] 判斷：
  // kind 剛好是 "toString" 這類內建名稱時，直接取值會拿到函式，而不是 undefined
  if (!Object.hasOwn(REVIEW_KINDS, kind)) {
    throw new Error(`[reviewKinds] 不認得的評論種類：${kind}`);
  }
  return REVIEW_KINDS[kind];
}
