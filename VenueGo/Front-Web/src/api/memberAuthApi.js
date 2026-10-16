import http from "./http";

// 會員登入：POST /api/member/auth/login
export async function memberLogin(email, password) {
  const { data } = await http.post("/member/auth/login", {
    email,
    password,
  });

  return data;
}

// 取得目前登入會員：GET /api/member/auth/me
export async function getCurrentMember() {
  const { data } = await http.get("/member/auth/me");

  return data;
}

/**
 * 會員註冊：POST /api/member/auth/register
 */
export async function registerMember(payload) {
  const { data } = await http.post("/member/auth/register", payload);
  return data;
}

/**
 * 申請重設密碼
 * POST /api/member/auth/forgot-password
 */
export async function forgotPassword(email) {
  const response = await http.post("/member/auth/forgot-password", {
    email,
  });

  return {
    success: true,
    message: response.message,
    data: response.data,
  };
}

/**
 * 驗證重設 Token
 * POST /api/member/auth/validate-reset-token
 */
export async function validateResetToken(email, token) {
  const response = await http.post("/member/auth/validate-reset-token", {
    email,
    token,
  });

  return {
    success: true,
    message: response.message,
    data: response.data,
  };
}

/**
 * 重設會員密碼
 * POST /api/member/auth/reset-password
 */
export async function resetMemberPassword({ email, token, password, confirmPassword }) {
  const response = await http.post("/member/auth/reset-password", {
    email,
    token,
    password,
    confirmPassword,
  });

  return {
    success: true,
    message: response.message,
    data: response.data,
  };
}
