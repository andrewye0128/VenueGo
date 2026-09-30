// API 錯誤代碼：對應後端 ApiResultVM.Fail(message, errorCode) 的 errorCode
// 頁面判斷錯誤時請用這裡的常數（例如 error.errorCode === ErrorCodes.NotFound），
// 不要直接寫字串，也不要比對中文訊息（中文訊息之後可能會改）
// 後端新增 errorCode 時，請同步補在這裡；命名沿用後端的大駝峰寫法
export const ErrorCodes = Object.freeze({
  // ── 後端回傳 ──
  NotFound: "NotFound", // 找不到資料

  // ── 前台 src/api/http.js 產生（不是後端回傳）──
  ValidationFailed: "ValidationFailed", // [ApiController] 自動產生的輸入驗證錯誤
});
