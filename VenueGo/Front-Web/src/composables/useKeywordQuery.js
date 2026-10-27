// 列表頁的「關鍵字」跟網址的 ?keyword= 同步：「我的預約」「訂單與付款」共用
//
// 用途：兩頁用訂單編號互相跳轉，例如點「我的預約」的訂單編號 →
//   /member/orders?keyword=VG20261015001，訂單頁一打開就只查這一筆
//
// 用法（在頁面的 setup 裡、onMounted(load) 之前呼叫）：
//   const filters = ref({ ...DEFAULT_FILTERS });
//   useKeywordQuery(filters);
//
// 同步的方向：
//   網址 → 條件：進入頁面時、網址改變時（例如從側邊選單再點一次，網址的 keyword 不見了）
//   條件 → 網址：按「查詢」「重置」改到關鍵字時。網址跟畫面一致，重新整理才不會跑回剛剛跳轉帶來的那一筆
import { watch } from "vue";
import { useRoute, useRouter } from "vue-router";

export function useKeywordQuery(filters) {
  const route = useRoute();
  const router = useRouter();

  // ?keyword=a&keyword=b 這種重複的參數，vue-router 會給陣列，這裡只接受字串
  const keywordInRoute = () =>
    typeof route.query.keyword === "string" ? route.query.keyword.trim() : "";

  // 進入頁面：直接寫進條件，不會觸發重新查詢（watch 還沒建立）
  // 篩選區的欄位（ListFilterPanel 的 draft）是用這時候的條件建立的，所以搜尋框也會顯示這個關鍵字
  filters.value.keyword = keywordInRoute();

  // 網址 → 條件。比較時要 trim：條件裡的關鍵字可能前後有空白，網址裡的已經去掉了
  watch(
    () => route.query.keyword,
    () => {
      const keyword = keywordInRoute();
      if (keyword !== filters.value.keyword.trim()) {
        filters.value = { ...filters.value, keyword };
      }
    },
  );

  // 條件 → 網址。用 replace 不用 push：每查一次就多一筆「上一頁」紀錄的話，按上一頁要按很多次才離得開
  // keyword 是空的就給 undefined，網址上就不會出現 ?keyword=
  watch(
    () => filters.value.keyword,
    (value) => {
      const keyword = value.trim();
      if (keyword === keywordInRoute()) return;
      router.replace({ query: { ...route.query, keyword: keyword || undefined } });
    },
  );
}
