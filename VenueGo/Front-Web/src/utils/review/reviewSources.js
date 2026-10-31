// 撰寫頁的「← 回…」按鈕要回哪裡（昱，10/31）
// 來源寫在網址的 ?from=，對照表在 constants/review.js 的 REVIEW_SOURCES。
import { REVIEW_SOURCES } from "@/constants/review";

/** ?from= 對應的返回按鈕：{ backLabel, to } */
export function reviewSource(from) {
  if (from == null || from === "") return REVIEW_SOURCES.reviews; // 沒帶：從「我的評論」來
  if (Object.hasOwn(REVIEW_SOURCES, from)) return REVIEW_SOURCES[from];
  return REVIEW_SOURCES.reviews; // 不認得的值（網址被改過、或 from=write）：回我的評論
}
