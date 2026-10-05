// 全站共用的 axios：網址開頭、逾時時間、錯誤處理只寫這一次
// 所有 API 檔案都從這裡 import，不要在各檔案直接 import axios
import axios from "axios";
import { ErrorCodes } from "@/constants/errorCodes";

const http = axios.create({
  // 用相對路徑，由 Vite 轉發給後端；不要寫死 https://localhost:7078
  baseURL: "/api",
  // 10 秒沒回應就當成失敗
  timeout: 10000,
});

// 讓後端知道「這是程式發的請求」（昱）：沒登入時 ASP.NET Core 會回 401，而不是把請求導向登入頁。
// Program.cs 已經讓 /api 開頭的網址直接回 401，這行是雙重保險。
http.defaults.headers.common["X-Requested-With"] = "XMLHttpRequest";

// ── 防偽 token（昱）────────────────────────────────────────
// 後端的 POST／PUT／DELETE 有 [AutoValidateAntiforgeryToken] 時，要帶防偽 token 才會通過。
// 流程：第一次要送這類請求時，先 GET /api/antiforgery/token，後端會把 token 寫進 XSRF-TOKEN 這個 Cookie；
//      之後每次都從 Cookie 讀出來，放進 RequestVerificationToken 標頭（ASP.NET Core 預設會找的標頭名稱）。
// 登入、登出之後舊的 token 會失效（後端回 400），下面的錯誤處理會自動重拿一次、重送一次。
const XSRF_COOKIE = "XSRF-TOKEN"; // 名稱跟後端 AntiforgeryApiController.CookieName 一致
const XSRF_HEADER = "RequestVerificationToken";
const UNSAFE_METHODS = ["post", "put", "patch", "delete"];

function readCookie(name) {
  const row = document.cookie.split("; ").find((r) => r.startsWith(name + "="));
  return row ? decodeURIComponent(row.slice(name.length + 1)) : null;
}

// 同一時間只拿一次：好幾個 POST 同時發生時，共用同一個結果
let tokenRequest = null;
function refreshXsrfToken() {
  if (!tokenRequest) {
    tokenRequest = http.get("/antiforgery/token").finally(() => {
      tokenRequest = null;
    });
  }
  return tokenRequest.then(() => readCookie(XSRF_COOKIE));
}

http.interceptors.request.use(async (config) => {
  if (UNSAFE_METHODS.includes((config.method ?? "get").toLowerCase())) {
    const token = readCookie(XSRF_COOKIE) ?? (await refreshXsrfToken());
    if (token) config.headers[XSRF_HEADER] = token;
  }
  return config;
});

// 防偽 token 驗證失敗的樣子：400，而且不是 ApiResult、也沒有欄位錯誤（框架直接擋下來的）
const isAntiforgeryFailure = (error) =>
  error.response?.status === 400 &&
  UNSAFE_METHODS.includes((error.config?.method ?? "").toLowerCase()) &&
  !isApiResult(error.response.data) &&
  !error.response.data?.errors;

// 判斷是不是後端的 ApiResult 格式：{ success, message, errorCode, data }
const isApiResult = (body) =>
  body !== null && typeof body === "object" && typeof body.success === "boolean";

http.interceptors.response.use(
  // ── 成功（HTTP 2xx）──
  (response) => {
    const body = response.data;
    if (isApiResult(body)) {
      // 防呆：萬一後端回 200 但 success 是 false，也當成錯誤
      if (!body.success) {
        const error = new Error(body.message || "操作失敗");
        error.errorCode = body.errorCode;
        return Promise.reject(error);
      }
      // 把 ApiResult 裡的 data 取出來，API 函式寫 const { data } = ... 就是真正的資料
      response.data = body.data;
      // 後端的成功訊息（例如「回覆已送出」），需要時可以顯示
      response.message = body.message;
    }
    // 不是 ApiResult 格式的回應（舊的 API）原封不動
    return response;
  },

  // ── 失敗（HTTP 4xx / 5xx、連不到伺服器）──
  // 統一整理成：error.message（給人看的中文）、error.errorCode（給程式判斷）
  (error) => {
    // 防偽 token 失效（通常是拿 token 之後登入或登出了）：重拿一次、重送一次（昱）
    // 只重試一次（_xsrfRetried），避免真的壞掉時無限重送
    if (isAntiforgeryFailure(error) && !error.config._xsrfRetried) {
      error.config._xsrfRetried = true;
      return refreshXsrfToken().then(() => http.request(error.config));
    }
    const body = error.response?.data;
    if (!error.response) {
      // 沒有收到回應：後端沒開、網路斷線、逾時
      error.message = "無法連線到伺服器，請稍後再試";
    } else if (isApiResult(body)) {
      // 格式 1：後端用 ApiResultVM.Fail(message, errorCode) 回傳，直接用後端寫好的訊息
      error.message = body.message || "操作失敗";
      error.errorCode = body.errorCode;
    } else if (body?.errors) {
      // 格式 2：[ApiController] 自動產生的驗證錯誤（ProblemDetails），取第一則訊息
      error.message = Object.values(body.errors).flat()[0] || "輸入資料有誤";
      error.errorCode = ErrorCodes.ValidationFailed;
    } else if (isAntiforgeryFailure(error)) {
      // 重拿 token 之後還是失敗（昱）
      error.message = "頁面已經過期，請重新整理後再試一次";
      error.errorCode = ErrorCodes.PageExpired;
    } else if (error.response.status === 401) {
      // 後端沒有回 ApiResult 的 401（昱）
      error.message = "請先登入";
      error.errorCode = ErrorCodes.NotLoggedIn;
    } else if (error.response.status === 403) {
      error.message = "這個功能需要其他身分才能使用";
      error.errorCode = ErrorCodes.Forbidden;
    } else if (error.response.status === 404) {
      error.message = "找不到要求的資料";
    } else if (error.response.status >= 500) {
      error.message = "伺服器發生錯誤，請稍後再試";
    }
    return Promise.reject(error);
  },
);

export default http;
