import http from "./http";

// 我的票券：GET /api/tickets/mine
// 登入還沒串好前，開發時用 testUserId 假裝某個會員
export async function getMyTickets(testUserId) {
  const { data } = await http.get("/tickets/mine", { params: { testUserId } });
  return data;
  // 直接回傳apiresult的內部回傳結果
}

// 收到的轉讓：GET /api/tickets/incoming（不含驗證碼，碼只在信裡）
export async function getIncomingTransfers() {
  const { data } = await http.get("/tickets/incoming");
  return data;
}

// 使用票券視窗：GET /api/tickets/{id}/qr
//  成功回傳 { payload, expiresAt, status }（status 是 available 或 inside）
export async function getTicketQr(ticketId) {
  const { data } = await http.get(`/tickets/${ticketId}/qr`);
  return data;
}

export async function createTransfer(ticketId, email) {
  const { data } = await http.post(`/tickets/${ticketId}/transfer`, { email });
  return data;
}

export async function resendTransfer(ticketId) {
  const { data } = await http.post(`/tickets/${ticketId}/transfer/resend`);
  return data;
}

export async function cancelTransfer(ticketId) {
  const { data } = await http.delete(`/tickets/${ticketId}/transfer`);
  return data;
}

export async function redeemTransfer(code) {
  await http.post("/tickets/redeem", { code });
}

export async function GetCurrentPeopleCount() {
  const { data } = await http.get("/tickets/venue-headcount");
  return data;
}
