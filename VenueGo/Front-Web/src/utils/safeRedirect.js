// 檢查 ?redirect= 的網址能不能用：只接受本網站內的路徑（"/" 開頭），其他一律回傳 null
// 防止「開放式重新導向」：壞人做一個 /login?redirect=https://假網站 的連結，
// 客人看到網址是 VenueGo 放心登入，登入後卻被帶到假網站
// "//假網站"、"/\假網站" 瀏覽器也會當成外部網址，所以也要擋
// 使用的地方：路由守衛（已登入就導走）、登入頁（登入成功後）、暫時無法連線頁（重試）
export function safeRedirect(value) {
  if (typeof value !== "string") return null;
  if (!value.startsWith("/") || value.startsWith("//") || value.startsWith("/\\")) return null;
  return value;
}
