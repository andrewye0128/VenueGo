import http from "./http";

// 常見問題頁：GET /api/faqs
// 一次回傳全部分類（順序同後台），切換頁籤只在前端切換，不用再呼叫
// 回傳 [{ category, categoryName, faqs: [{ id, question, answerSegments }] }]
//   沒有問題的分類也會回傳，faqs 是空陣列
//   answerSegments：[{ type: "text" | "link", text, url, isExternal }]
//     text 片段可能含換行 \n，顯示時用 whitespace-pre-line；不要用 v-html
//     link 片段：isExternal 為 true 開新分頁，false 用 RouterLink 在同一頁切換
export async function getFaqs() {
  const { data } = await http.get("/faqs");
  return data;
}
