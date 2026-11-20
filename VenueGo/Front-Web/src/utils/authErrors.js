// 判斷 API 的錯誤是不是代表「登入已經失效，要請客人重新登入」
// 看 HTTP 401，不看 errorCode：NotLoggedIn（token 過期、被竄改）、
// InvalidToken（找不到這個會員）都是 401，以後後端新增其他 401 的代碼也不用改這裡
// 後端沒開、網路斷線時沒有 response，資料庫斷線是 500：都回傳 false（只是暫時出問題，不要登出）
export function isSessionInvalidError(error) {
  return error?.response?.status === 401;
}
