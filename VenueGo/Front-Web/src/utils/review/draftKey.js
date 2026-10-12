// 評論草稿的 localStorage key（昱，10/10 從 ReviewWriteView.vue 抽出來）
//
// 撰寫頁（存、讀草稿）和「我的評論 › 待評論」（顯示「有草稿」標籤）都要用同一個 key，
// 格式只寫在這裡一份：哪天要改版本號或格式，兩邊一起變，不會出現「明明有草稿，清單卻說沒有」。
//
// 格式：review-draft:v3:{會員 Id}:{kind}:{ticket}
//   v3（10/9）：加上會員 Id，共用電腦時下一個登入的人不會看到上一個人的草稿；
//               現場評論的 ticket 從 QRToken 改成憑證 Id。
//   kind、ticket：同撰寫頁的網址（visit／booking，憑證 Id 或訂單 Id）。

/** 草稿的 key。沒登入（userId 是 null）時用 guest，跟撰寫頁原本的寫法一樣。 */
export function reviewDraftKey(userId, kind, ticket) {
  return `review-draft:v3:${userId ?? "guest"}:${kind}:${ticket}`;
}

/**
 * 這張憑證有沒有存過草稿。
 * localStorage 在無痕模式的某些設定、或被瀏覽器封鎖時會直接丟例外。
 * 「有草稿」只是提示，壞掉就當作沒有，不能讓整頁跟著壞（同 useDraft.js 的處理）。
 */
export function hasReviewDraft(userId, kind, ticket) {
  try {
    return localStorage.getItem(reviewDraftKey(userId, kind, ticket)) != null;
  } catch {
    return false;
  }
}
