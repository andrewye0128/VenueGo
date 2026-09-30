// 顧客端評論的 API（規格：《API規格_CReview.md》）
// 每個函式對應一支端點；頁面只呼叫這裡的函式，不自己拼網址——網址規則只寫在這一個檔案。
//
// kind   = "visit"（現場評論，ticket 是 QRToken）
//        | "booking"（預約評論，ticket 是 ReviewPerBookingId，要會員本人登入）
//
// 共用的 http.js 會自動取出 ApiResult 的 data，所以這裡的函式回傳的就是真正的資料。
// 失敗時丟出的錯誤有 error.message（中文）與 error.errorCode（對照 @/constants/errorCodes）。
import http from "./http";

// QRToken 理論上只有英數字，但包一層 encodeURIComponent 是習慣：
// 萬一哪天格式改了、出現 / 或 ? 之類的字元，網址才不會斷掉。
function base(kind, ticket) {
  return `/reviews/${kind}/${encodeURIComponent(ticket)}`;
}

/** 評論專區（公開卡片）。filter 的欄位見規格 2-1，沒給的由後端補預設值。 */
export async function getPublicReviews(filter) {
  const { data } = await http.get("/reviews", { params: filter });
  return data;
}

/** 撰寫頁要顯示的東西，同時判定資格（過期、已評過、查無憑證）。 */
export async function getWriteForm(kind, ticket) {
  const { data } = await http.get(`${base(kind, ticket)}/form`);
  return data;
}

/** 送出評論。input 見規格 2-3。回傳後端的成功訊息（「評論已送出」）。 */
export async function createReview(kind, ticket, input) {
  const response = await http.post(base(kind, ticket), input);
  return response.message;
}

/** 我的評論（檢視頁）。⚠️ 這支是純讀取，不會記錄「顧客已看過回覆」。 */
export async function getMyReview(kind, ticket) {
  const { data } = await http.get(base(kind, ticket));
  return data;
}

/** 員工預覽（規格 2-8）：只有後台角色打得開，回傳的形狀跟「我的評論」一樣。 */
export async function getReviewPreview(reviewId) {
  const { data } = await http.get(`/reviews/preview/${encodeURIComponent(reviewId)}`);
  return data;
}

/** 記錄「顧客已看過館方回覆」。重複呼叫不會覆蓋第一次的時間。 */
export async function markReplyViewed(kind, ticket) {
  const { data } = await http.post(`${base(kind, ticket)}/reply-viewed`);
  return data;
}

/** 切換公開。只有現場評論有這支。 */
export async function setVisibility(token, isPublic) {
  const { data } = await http.post(`${base("visit", token)}/visibility`, { isPublic });
  return data;
}

/** 對館方回覆表態：0 不滿意 / 1 普通 / 2 滿意 */
export async function setSatisfaction(kind, ticket, satisfaction) {
  const { data } = await http.post(`${base(kind, ticket)}/satisfaction`, { satisfaction });
  return data;
}

/**
 * 從錯誤裡取出「各欄位的錯誤訊息」：{ starRating: ["請選擇星等"] }，沒有就是空物件。
 * 後端有兩種形狀，兩種都接：
 *   1. ApiResult（Program.cs 有掛 ApiResponses.InvalidModelState 時）：data.errors，欄位已經是小寫開頭
 *   2. ProblemDetails（沒掛時，框架預設）：errors，欄位是大寫開頭（StarRating）
 */
export function fieldErrorsOf(error) {
  const body = error.response?.data;
  const raw = body?.data?.errors ?? body?.errors ?? {};
  const result = {};
  for (const [key, messages] of Object.entries(raw)) {
    const name = key.replace(/^\$\.?/, "");
    const camel = name ? name.charAt(0).toLowerCase() + name.slice(1) : "_";
    result[camel] = Array.isArray(messages) ? messages : [String(messages)];
  }
  return result;
}
