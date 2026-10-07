// 我的預約 API
// ⚠️ 目前用假資料（mocks/myReservations.js）模擬後端的篩選、排序、分頁、取消
// 串接時只要把兩個函式的內容換成呼叫後端，頁面和元件都不用改：
//   getMyReservations    → http.get("/reservations/mine", { params })
//   cancelMyReservation  → http.post(`/reservations/${reservationId}/cancel`, { reason })
// （網址是暫定的，要等後端 API 規格確定）
import { myReservations } from "@/mocks/myReservations";
import { ALL } from "@/constants/statuses";
import { formatReservationNo } from "@/utils/reservation";

// 模擬網路等待 0.3 秒，畫面才看得到「載入中」
const delay = (ms = 300) => new Promise((resolve) => setTimeout(resolve, ms));

// 回傳複本：頁面拿到的資料被改到時，不會影響假資料本身（真的 API 每次也都是新的資料）
const copy = (item) => ({ ...item });

/**
 * 取得我的預約（篩選＋分頁）
 * @param {object} params
 * @param {string} params.keyword 比對場地名稱、預約編號（R012、r012、12 都找得到 R012）
 * @param {string|null} params.date 預約日期 "2026-10-08"；null 表示不篩選
 * @param {number|"all"} params.reservationStatus 預約狀態；"all" 表示不篩選
 * @param {number|"all"} params.paymentStatus 付款狀態；"all" 表示不篩選
 * @param {number} params.page 第幾頁（從 1 開始）
 * @param {number} params.pageSize 每頁幾筆
 * @returns {Promise<{ items: object[], totalCount: number, page: number, pageSize: number }>}
 */
export async function getMyReservations({
  keyword = "",
  date = null,
  reservationStatus = ALL,
  paymentStatus = ALL,
  page = 1,
  pageSize = 10,
} = {}) {
  await delay();

  const word = keyword.trim().toLowerCase();
  const filtered = myReservations.filter(
    (r) =>
      (!word ||
        r.venueName.toLowerCase().includes(word) ||
        formatReservationNo(r.reservationId).toLowerCase().includes(word)) &&
      (!date || r.bookingDate === date) &&
      (reservationStatus === ALL || r.reservationStatus === reservationStatus) &&
      (paymentStatus === ALL || r.paymentStatus === paymentStatus),
  );

  // 預約日期由新到舊；同一天的話，時間晚的在前（"2026-10-08" 這種格式可以直接比字串）
  filtered.sort(
    (a, b) => b.bookingDate.localeCompare(a.bookingDate) || b.startTime.localeCompare(a.startTime),
  );

  const start = (page - 1) * pageSize;
  return {
    items: filtered.slice(start, start + pageSize).map(copy),
    totalCount: filtered.length,
    page,
    pageSize,
  };
}

/**
 * 取消預約
 * 狀態變化跟後台的取消功能（ReservationCommandService）一致：
 *   預約 → 已取消；未付款 → 付款取消；已付款 → 退款處理中（退款流程還在討論，先照後台規則）
 * @param {number} reservationId
 * @param {string|null} reason 取消原因（選填，最多 200 字）
 */
export async function cancelMyReservation(reservationId, reason = null) {
  await delay();

  const target = myReservations.find((r) => r.reservationId === reservationId);
  if (!target) throw new Error("找不到這筆預約");
  if (![0, 1].includes(target.reservationStatus)) throw new Error("這筆預約目前無法取消");

  // 取消時間：現在（"2026-10-07T14:30:00"，跟 C# DateTime 的格式一樣、沒有時區）
  const now = new Date();
  const pad = (n) => String(n).padStart(2, "0");
  const cancelledAt = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T${pad(now.getHours())}:${pad(now.getMinutes())}:00`;

  target.reservationStatus = 2; // 已取消
  target.paymentStatus = target.paymentStatus === 2 ? 5 : 4; // 已付款 → 退款處理中；其他 → 付款取消
  target.cancelledAt = cancelledAt;
  // 沒填原因時填「會員自行取消」（假設；之後由後端決定）
  target.cancelReason = reason || "會員自行取消";
}
