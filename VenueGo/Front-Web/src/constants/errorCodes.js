// API 錯誤代碼：對應後端 ApiResultVM.Fail(message, errorCode) 的 errorCode
// 頁面判斷錯誤時請用這裡的常數（例如 error.errorCode === ErrorCodes.NotFound），
// 不要直接寫字串，也不要比對中文訊息（中文訊息之後可能會改）
// 後端新增 errorCode 時，請同步補在這裡；命名沿用後端的大駝峰寫法
export const ErrorCodes = Object.freeze({
  // ── 後端回傳 ──
  NotFound: "NotFound", // 找不到資料
  NotLoggedIn: "NotLoggedIn", // 沒登入（/api 開頭的網址，後端 ApiResponses.RedirectToLogin 回的）
  Forbidden: "Forbidden", // 登入了但身分不對，例如員工帳號打會員專用的 API

  // ── 評論（昱）──
  Expired: "Expired", // 超過可以評論的時間
  AlreadyReviewed: "AlreadyReviewed", // 這張憑證已經評論過了
  AlreadyRated: "AlreadyRated", // 已經對館方回覆表態過了
  NoReply: "NoReply", // 館方還沒回覆，不能表態
  SpamMarked: "SpamMarked", // 評論已下架，不能切換公開
  InvalidSatisfaction: "InvalidSatisfaction", // 表態的值不是 0／1／2

  // ── 前台 src/api/http.js 產生（不是後端回傳）──
  ValidationFailed: "ValidationFailed", // [ApiController] 自動產生的輸入驗證錯誤
  PageExpired: "PageExpired", // 防偽 token 驗證失敗，重拿一次也不行（昱）
});
