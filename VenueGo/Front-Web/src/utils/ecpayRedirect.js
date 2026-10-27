// 把後端開好的付款單交給綠界：畫面會整頁跳到綠界的刷卡頁
//
// 為什麼用表單、不用 axios：綠界規定要用瀏覽器「整頁送出表單」才會換到刷卡頁，
// axios 送出去的結果只會回到程式裡，畫面不會換；綠界也不允許把刷卡頁放在 iframe 裡

// 只接受綠界全方位金流的付款網址（測試、正式各一個）
// 萬一後端設定打錯或回應被竄改，不會把客人帶到別的網站輸入卡號
const ALLOWED_CHECKOUT_URLS = [
  "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5",
  "https://payment.ecpay.com.tw/Cashier/AioCheckOut/V5",
];

/**
 * 送出付款單，跳到綠界刷卡頁
 * fields 原封不動地送：欄位名稱的大小寫、值都不能改，改一個字 CheckMacValue（防偽章）就對不上，綠界會拒絕
 * @param {{ actionUrl: string, fields: Record<string, string> }} checkout startPayment() 的回傳
 * @throws {Error} 網址不是綠界的付款網址（不送出）
 */
export function submitToEcpay({ actionUrl, fields }) {
  if (!ALLOWED_CHECKOUT_URLS.includes(actionUrl)) {
    throw new Error("付款網址異常，請稍後再試或改到櫃台付款");
  }

  const form = document.createElement("form");
  form.method = "POST";
  form.action = actionUrl;
  // 商品名稱有中文，用 UTF-8 送才會跟後端計算防偽章時一致
  form.acceptCharset = "UTF-8";
  form.hidden = true;

  for (const [name, value] of Object.entries(fields)) {
    const input = document.createElement("input");
    input.type = "hidden";
    input.name = name;
    // 用 .value 設定：值裡有 < > " 之類的字元也只是文字，不會變成 HTML
    input.value = value;
    form.append(input);
  }

  // 表單要放進頁面裡才能送出（部分瀏覽器不接受送出不在頁面上的表單）
  document.body.append(form);
  form.submit();
}
