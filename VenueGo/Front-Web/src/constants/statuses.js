// 狀態對照表：後端回傳的數字 → 畫面上的文字、顏色、圖示
// ⚠️ 數字要跟後端 Models/Enums 一致（ReservationStatus.cs、PaymentStatus .cs），後端新增狀態時這裡也要補
// color 是 Nuxt UI 的顏色名稱：success 綠、warning 橘、error 紅、primary 藍、neutral 灰（對應設計規範，見 main.css）
// 圖示名稱要寫完整，Nuxt UI 打包時才掃描得到

// 預約狀態（Reservations.ReservationStatus）
export const RESERVATION_STATUS = {
  0: { label: "待確認", color: "warning", icon: "i-mdi-clock-outline" },
  1: { label: "已確認", color: "success", icon: "i-mdi-check-circle" },
  2: { label: "已取消", color: "neutral", icon: "i-mdi-close-circle" },
  3: { label: "已逾期", color: "neutral", icon: "i-mdi-clock-alert-outline" },
  4: { label: "已完成", color: "primary", icon: "i-mdi-flag-checkered" },
  6: { label: "場館取消", color: "error", icon: "i-mdi-cancel" },
  7: { label: "已作廢", color: "neutral", icon: "i-mdi-file-cancel-outline" },
};

// 付款狀態（Payments.PaymentStatus）：我的預約、訂單與付款共用
export const PAYMENT_STATUS = {
  0: { label: "未付款", color: "error", icon: "i-mdi-alert-circle" },
  1: { label: "付款處理中", color: "warning", icon: "i-mdi-progress-clock" },
  2: { label: "已付款", color: "success", icon: "i-mdi-credit-card-check-outline" },
  3: { label: "付款失敗", color: "error", icon: "i-mdi-close-circle" },
  4: { label: "付款取消", color: "neutral", icon: "i-mdi-credit-card-off-outline" },
  5: { label: "退款處理中", color: "primary", icon: "i-mdi-cash-refund" },
  6: { label: "已退款", color: "primary", icon: "i-mdi-history" },
};

// 篩選用的「全部」：USelect 的值不能是空字串或 null，所以用 "all"
export const ALL = "all";

// 對照表 → USelect 的選項，第一個是「全部」
const toOptions = (statusMap) => [
  { label: "全部", value: ALL },
  ...Object.entries(statusMap).map(([value, { label }]) => ({ label, value: Number(value) })),
];

export const RESERVATION_STATUS_OPTIONS = toOptions(RESERVATION_STATUS);
export const PAYMENT_STATUS_OPTIONS = toOptions(PAYMENT_STATUS);
