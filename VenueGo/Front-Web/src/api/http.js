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
// ── 送出請求前：已登入就自動帶上 JWT ──
// 各 API 檔案不用自己處理 token，之後登入資訊有調整也只要改這個檔案
http.interceptors.request.use((config) => {
  const token = localStorage.getItem("token");

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});
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
    } else if (error.response.status === 404) {
      error.message = "找不到要求的資料";
    } else if (error.response.status >= 500) {
      error.message = "伺服器發生錯誤，請稍後再試";
    }
    return Promise.reject(error);
  },
);

export default http;
