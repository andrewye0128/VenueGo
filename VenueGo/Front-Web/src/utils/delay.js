// 等待一段時間：await delay(300)
// 目前只用在假資料「模擬網路等待」（串接真的 API 後就不需要了）
//
// 為什麼要用 new Promise 包一層：
//   setTimeout 是「時間到了呼叫一個函式」的舊式寫法，本身不回傳 Promise，不能直接 await，
//   所以在這裡包一次，其他地方就能寫成 await delay(ms)
export const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
