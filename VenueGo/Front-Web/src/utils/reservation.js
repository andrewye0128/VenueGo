// 預約相關的共用規則：表格、手機卡片、明細視窗都用這裡的函式，規則只寫一次

// 預約編號：R + ReservationId，補到 3 位數（R001、R012、R123；超過 3 位數照原本位數，R1234）
export function formatReservationNo(reservationId) {
  return `R${String(reservationId).padStart(3, "0")}`;
}

// 能不能取消：待確認（0）或已確認（1），而且還沒到開始使用的時間
// 跟後端 EnumExtensions.IsActive() 的「有效預約」規則一致；取消期限是營運決定的「開始使用前都可以取消」
// ⚠️ 這裡只決定「要不要顯示取消按鈕」，真正能不能取消要由後端 API 再檢查一次
const CANCELLABLE_STATUSES = [0, 1];

export function canCancel(reservation, now = new Date()) {
  if (!CANCELLABLE_STATUSES.includes(reservation.reservationStatus)) return false;
  // "2026-10-08T18:00" 這種「有日期也有時間、沒有時區」的字串，JavaScript 會當成本地時間（台灣時間）
  // 注意：只有日期的 "2026-10-08" 反而會被當成國際標準時間，所以一定要把時間一起組進去
  const startAt = new Date(`${reservation.bookingDate}T${reservation.startTime.slice(0, 5)}`);
  return startAt > now;
}
