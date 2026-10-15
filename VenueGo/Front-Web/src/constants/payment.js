// 付款方式、發票類型：建立預約時送給後端的數字
// ⚠️ 數字要跟後端 Models/Enums 一致（PaymentMethod.cs、InvoiceType.cs），後端改了這裡也要改

// 付款方式（Payments.PaymentMethod）
export const PAYMENT_METHOD = Object.freeze({
  ONSITE: 0, // 現場付款（到櫃台付）
  ONLINE: 1, // 線上付款（信用卡，階段 3 做模擬付款頁）
});

export const PAYMENT_METHOD_LABEL = {
  [PAYMENT_METHOD.ONSITE]: "現場付款",
  [PAYMENT_METHOD.ONLINE]: "線上付款",
};

// 發票類型（Orders.InvoiceType）
export const INVOICE_TYPE = Object.freeze({
  PAPER: 0, // 現場索取紙本發票
  MOBILE_BARCODE: 1, // 手機條碼載具
});

export const INVOICE_TYPE_LABEL = {
  [INVOICE_TYPE.PAPER]: "紙本發票（現場索取）",
  [INVOICE_TYPE.MOBILE_BARCODE]: "手機條碼載具",
};

// 手機條碼載具格式：斜線＋7 碼大寫英數字或 + - . 三個符號（例如 /AB12345）
// 跟後端 ReservationCreationService 的 CarrierNoPattern 一樣
export const CARRIER_NO_PATTERN = /^\/[0-9A-Z+\-.]{7}$/;
