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
