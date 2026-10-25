// 顧客端評論的 API（規格：《API規格_CReview.md》）
// 每個函式對應一支端點；頁面只呼叫這裡的函式，不自己拼網址——網址規則只寫在這一個檔案。
//
// kind   = "visit"（現場評論）| "booking"（預約評論）
// ticket = 訂單 Id（OrderId）。10/15 Reviews 重構起兩種都是訂單 Id（以前現場評論是憑證 Id），
//          變數名稱沿用，呼叫的頁面不用改。
// 10/25：變數名稱也改成 orderId（後端的網址參數、待評論／已評論清單的欄位一起改名），網址本身不變。
// 10/9 起兩種都要會員本人登入（http.js 會自動帶上 JWT）。
//
// 共用的 http.js 會自動取出 ApiResult 的 data，所以這裡的函式回傳的就是真正的資料。
// 失敗時丟出的錯誤有 error.message（中文）與 error.errorCode（對照 @/constants/errorCodes）。
//
// 這裡的函式都不 catch，錯誤由呼叫的頁面處理：
// 錯誤訊息 http.js 已經整理成中文（欄位錯誤在 error.fieldErrors），要顯示在哪裡、要不要重試，只有頁面知道。
import http from "./http";

// ticket 現在都是數字，但包一層 encodeURIComponent 是習慣：
// 萬一哪天格式改了、出現 / 或 ? 之類的字元，網址才不會斷掉。
// 10/25 改名 ticket → orderId
function base(kind, orderId) {
  // 10/25 改名 ticket → orderId
  return `/reviews/${kind}/${encodeURIComponent(orderId)}`;
}

// 首頁顧客評價輪播：[GET] /api/reviews/featured（規格 2-11，10/15）
// 回傳 { summary, items }：items 最多 12 則，後端已經挑好、打亂順序，照順序顯示就好。
export async function getFeaturedReviews() {
  const { data } = await http.get("/reviews/featured");
  return data;
}

// 我的預約、我的票券的評論按鈕：[GET] /api/reviews/mine/status（規格 2-12，10/15）
// 回傳陣列，每筆 { orderId, reservationId, visitAction, visitTicketId, bookingAction }，
// action 是 "write"（立刻評論）／"view"（查看評論）／null（不顯示）。
export async function listMyReviewStatuses() {
  const { data } = await http.get("/reviews/mine/status");
  return data;
}

// 評論專區（公開卡片）：[GET] /api/reviews
// filter 的欄位見規格 2-1，沒給的由後端補預設值。
export async function getPublicReviews(filter) {
  const { data } = await http.get("/reviews", { params: filter });
  return data;
}

// 撰寫頁的表單設定，同時判定資格（過期、已評過、查無憑證）：[GET] /api/reviews/{kind}/{orderId}/form
// 10/25 改名 ticket → orderId
export async function getWriteForm(kind, orderId) {
  // 10/25 改名 ticket → orderId
  const { data } = await http.get(`${base(kind, orderId)}/form`);
  return data;
}

// 送出評論：[POST] /api/reviews/{kind}/{orderId}
// input 見規格 2-3。回傳後端的成功訊息（「評論已送出」）。
// 10/25 改名 ticket → orderId
export async function createReview(kind, orderId, input) {
  // 10/25 改名 ticket → orderId
  const response = await http.post(base(kind, orderId), input);
  return response.message;
}

// 我的評論（檢視頁）：[GET] /api/reviews/{kind}/{orderId}
// ⚠️ 這支是純讀取，不會記錄「顧客已看過回覆」。
// 10/25 改名 ticket → orderId
export async function getMyReview(kind, orderId) {
  // 10/25 改名 ticket → orderId
  const { data } = await http.get(base(kind, orderId));
  return data;
}

// 我的評論 › 待評論（規格 2-9）：[GET] /api/reviews/mine/pending
// 本人的、還沒評、還沒過期的現場和預約憑證，快到期的排前面。每筆的 kind＋orderId 就是撰寫頁的網址（10/25 欄位從 ticket 改名）。
export async function listPendingReviews() {
  const { data } = await http.get("/reviews/mine/pending");
  return data;
}

// 我的評論 › 已評論（規格 2-10）：[GET] /api/reviews/mine/reviewed
// 本人寫過的評論摘要，新的排前面。完整內容展開時再用 getMyReview 讀。
// ⚠️ 純讀取，不會記錄「顧客已看過回覆」。
export async function listReviewedReviews() {
  const { data } = await http.get("/reviews/mine/reviewed");
  return data;
}

// 員工預覽（規格 2-8）：[GET] /api/reviews/preview/{reviewId}
// 只有後台角色打得開，回傳的形狀跟「我的評論」一樣。
export async function getReviewPreview(reviewId) {
  const { data } = await http.get(`/reviews/preview/${encodeURIComponent(reviewId)}`);
  return data;
}

// 記錄「顧客已看過館方回覆」：[POST] /api/reviews/{kind}/{orderId}/reply-viewed
// 重複呼叫不會覆蓋第一次的時間。
// 10/25 改名 ticket → orderId
export async function markReplyViewed(kind, orderId) {
  // 10/25 改名 ticket → orderId
  const { data } = await http.post(`${base(kind, orderId)}/reply-viewed`);
  return data;
}

// 切換公開：[POST] /api/reviews/visit/{orderId}/visibility
// 只有現場評論有這支。
// 10/25 改名 id → orderId
export async function setVisibility(orderId, isPublic) {
  // 10/25 改名 id → orderId
  const { data } = await http.post(`${base("visit", orderId)}/visibility`, { isPublic });
  return data;
}

// 對館方回覆表態：[POST] /api/reviews/{kind}/{orderId}/satisfaction
// satisfaction：0 不滿意 / 1 普通 / 2 滿意
// 10/25 改名 ticket → orderId
export async function setSatisfaction(kind, orderId, satisfaction) {
  // 10/25 改名 ticket → orderId
  const { data } = await http.post(`${base(kind, orderId)}/satisfaction`, { satisfaction });
  return data;
}
