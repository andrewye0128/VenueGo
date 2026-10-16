import http from "./http";

// 最新消息列表頁、首頁最新消息區塊：GET /api/news
// 回傳 { items, page, pageSize, totalCount }（全組前台 API 共用的分頁格式，總頁數由 UPagination 自己算）
export async function getNewsList({ category, page, pageSize } = {}) {
  const { data } = await http.get("/news", {
    params: { category, page, pageSize },
  });
  return data;
}

// 最新消息詳細頁：GET /api/news/{id}
// 回傳 { id, title, content, category, categoryName, publishedAt, imagePath, isPinned, previous, next }
// previous、next 沒有上一篇／下一篇時是 null
// 找不到（不存在、已刪除、還沒發布）時後端回 404，http.js 會拋出錯誤：
//   error.errorCode === "NEWS_NOT_FOUND"，由呼叫的頁面用 try...catch 處理
export async function getNewsDetail(id) {
  const { data } = await http.get(`/news/${id}`);
  return data;
}
