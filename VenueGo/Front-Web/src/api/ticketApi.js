import http from "./http";

// 我的票券：GET /api/tickets/mine
// 登入還沒串好前，開發時用 testUserId 假裝某個會員
export async function getMyTickets(testUserId) {
  try {
    const { data } = await http.get("/tickets/mine", { params: { testUserId } });
    return data;
  } catch (err) {
    console.error("getMyTickets 發生錯誤", err);
  }
}
