import { da } from "zod/locales";
import http from "./http";

// 我的票券：GET /api/tickets/mine
// 登入還沒串好前，開發時用 testUserId 假裝某個會員
export async function getMyTickets(testUserId) {
  const { data } = await http.get("/tickets/mine", { params: { testUserId } });
  return data;
  // 直接回傳apiresult的內部回傳結果
}

export async function createTransfer(ticketId) {
  const { data } = await http.post(`/tickets/${ticketId}/transfer`);
  return data;
}

export async function cancelTransfer(ticketId) {
  const { data } = await http.delete(`/tickets/${ticketId}/transfer`);
  return data;
}

export async function redeemTransfer(code) {
  await http.post("/tickets/redeem", { code });
}
