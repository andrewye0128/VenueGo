// API 錯誤代碼：對應後端 ApiResultVM.Fail(message, errorCode) 的 errorCode
// 頁面判斷錯誤時請用這裡的常數（例如 error.errorCode === ErrorCodes.NotFound），
// 不要直接寫字串，也不要比對中文訊息（中文訊息之後可能會改）
// 後端新增 errorCode 時，請同步補在這裡；命名沿用後端的大駝峰寫法
export const ErrorCodes = Object.freeze({
  // ── 後端回傳 ──
  NotFound: "NotFound", // 找不到資料
  LoginFailed: "LoginFailed", // 備用，理論上登入失敗現在都會有更明確的代碼

  // ── 會員登入（MemberAuthApiController）──
  InvalidCredentials: "InvalidCredentials", // 帳號不存在 / 密碼錯誤
  AccountLocked: "AccountLocked", // 密碼連續打錯，暫時鎖定中
  AccountSuspended: "AccountSuspended", // 帳號已被停權
  AccountInactive: "AccountInactive", // 帳號已註銷 / 狀態異常
  NotLoggedIn: "NotLoggedIn", // 沒登入（/api 開頭的網址，後端 ApiResponses.RedirectToLogin 回的）
  Forbidden: "Forbidden", // 登入了但身分不對，例如員工帳號打會員專用的 API

  // ── 會員個人資料（MemberProfileApiController）──
  UpdateProfileFailed: "UpdateProfileFailed", // 更新個人資料時伺服器發生錯誤

  // ── 會員註冊（MemberAuthApiController 的 register）──
  EmailAlreadyExists: "EmailAlreadyExists", // 這個 Email 已經註冊過（HTTP 409）
  RegistrationFailed: "RegistrationFailed", // 建立會員帳號時伺服器發生錯誤（HTTP 500）
  MemberRoleNotConfigured: "MemberRoleNotConfigured", // 系統缺少可用的會員角色（HTTP 500）

  // ── 評論（昱）──
  Expired: "Expired", // 超過可以評論的時間
  AlreadyReviewed: "AlreadyReviewed", // 這張憑證已經評論過了
  AlreadyRated: "AlreadyRated", // 已經對館方回覆表態過了
  NoReply: "NoReply", // 館方還沒回覆，不能表態
  SpamMarked: "SpamMarked", // 評論已下架，不能切換公開
  InvalidSatisfaction: "InvalidSatisfaction", // 表態的值不是 0／1／2

  // ── 場地預約（BookingApiController、ReservationApiController）──
  // 後端的常數在 Services/Reservations/IMemberBookingService.cs 的 BookingErrorCodes，改名時兩邊要一起改
  DateOutOfRange: "DateOutOfRange", // 日期不在會員可預約的範圍內（今天～30 天後）
  SlotUnavailable: "SlotUnavailable", // 時段不能訂：被訂走、不連續、超過 4 小時
  SlotTaken: "SlotTaken", // 送出的那一刻時段剛被別人訂走（409）
  VenueNotBookable: "VenueNotBookable", // 場地停用，或沒有啟用中的價格
  PaymentMethodNotAllowed: "PaymentMethodNotAllowed", // 付款方式不符合規則（例如 2 天內選現場付款）
  TermsVersionMismatch: "TermsVersionMismatch", // 沒同意條款，或條款改版了，要重新同意
  InvalidInput: "InvalidInput", // 填的內容有錯：人數超過容納人數、載具格式錯誤…（直接顯示後端的訊息）

  // ── 開始線上付款（OrderApiController 的 payment/start）──
  // 後端的值是 Models/PaymentModels/StartPaymentModels.cs 的 StartPaymentOutcome 名稱，改名時兩邊要一起改
  NotOnlinePayment: "NotOnlinePayment", // 現場付款的訂單，要到櫃台付
  AlreadyPaid: "AlreadyPaid", // 已經付款（或退款中、已退款）
  InProgress: "InProgress", // 上一筆付款還在確認中，不能再開一張付款單（避免付兩次）
  NotPayable: "NotPayable", // 預約已取消、已逾期等，不能付款
  PastStartDeadline: "PastStartDeadline", // 超過可以開始付款的時間（付款期限前幾分鐘）
  InvalidAmount: "InvalidAmount", // 訂單金額異常
  NotConfigured: "NotConfigured", // 網站沒有設定綠界，線上付款暫時不能用（503）
  Conflict: "Conflict", // 同一時間有另一個付款請求，這次被搶先了
  TradeNoUnavailable: "TradeNoUnavailable", // 交易編號一直撞號，暫時無法開付款單（503）

  // ── 前台 src/api/http.js 產生（不是後端回傳）──
  ValidationFailed: "ValidationFailed", // [ApiController] 自動產生的輸入驗證錯誤
  PageExpired: "PageExpired", // 防偽 token 驗證失敗，重拿一次也不行（昱）
});
