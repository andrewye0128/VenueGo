// ============================================================
//  useDraft.js — 表單草稿（取代 wwwroot/js/draft-box.js 的顧客端用途）
//
//  draft-box.js 的行為全部保留：
//    ✔ 草稿存在 localStorage，key 由呼叫端決定
//    ✔ 開啟頁面時自動代入，不詢問
//    ✔ 手動儲存；已經有草稿時先問要不要覆蓋
//    ✔ 送出成功才清掉草稿（驗證失敗、按了取消都不清）
//    ✔ 站內換頁 → 三選一（留下／不存直接走／存了再走）
//    ✔ 關分頁、F5 → 瀏覽器內建的兩選一
//
//  改變的只有「怎麼判斷離開時要不要攔」：
//    draft-box.js 是聽 input／change 事件，只要動過就算「改過」。
//    這裡問的是另一個問題：「現在離開，會不會弄丟東西？」
//    做法是在「要離開的那一刻」拿現在畫面上的值，去比對 localStorage 裡「真正存著的」草稿：
//      ・存著的草稿跟畫面一樣         → 不會丟東西，放行
//      ・沒有草稿，畫面也還是初始狀態 → 沒寫東西，放行
//      ・其他情況                     → 會丟東西，攔下來問
//
//  ⚠️ 為什麼每次都重新讀 localStorage，不記在變數裡（2026-09-25 修正）：
//     第一版是記住「上次按儲存時的內容」來比對。但草稿可能在頁面不知道的情況下消失——
//     使用者手動清掉、另一個分頁送出評論時清掉、瀏覽器清除網站資料⋯⋯
//     這時變數還以為「已經存過了」，就不攔了，結果畫面上的內容直接不見。
//     記在變數裡的是「頁面以為的狀態」，localStorage 裡的才是「真正的狀態」。
//
//  ⚠️ 館方端還在用 draft-box.js（Razor），那支檔案不要刪。
//
//  9/29 移植到 Front-Web：「已經有草稿，要覆蓋嗎？」原本用 window.confirm，
//  改成由頁面傳入 confirmOverwrite（回傳 Promise<boolean>），頁面用 Nuxt UI 的對話框問。
//  所以 save() 變成 async。
// ============================================================
import { ref, computed, onMounted, onBeforeUnmount, toValue } from "vue";
import { onBeforeRouteLeave } from "vue-router";

/**
 * @param {object}   options
 * @param {object}   options.form          reactive 物件，草稿讀寫的對象
 * @param {Function} options.key           回傳 localStorage key 的函式（資料載入後才確定，所以用函式）
 * @param {Function} options.fields        回傳要存哪些欄位名稱的函式（同上）
 * @param {Function} [options.confirmLeave] 站內換頁時呼叫，要回傳 Promise<'cancel'|'discard'|'save'>
 *                                          沒給的話退回 window.confirm 兩選一
 * @param {Function} [options.confirmOverwrite] 已經有草稿時呼叫，參數是舊草稿的儲存時間（已格式化），
 *                                          要回傳 Promise<boolean>（true＝覆蓋）。沒給的話退回 window.confirm
 */
export function useDraft({ form, key, fields, confirmLeave, confirmOverwrite }) {
  const savedAt = ref(null); // ISO 字串（機器讀）；顯示時才轉成人讀的格式
  let pristine = ""; // 剛載入、還沒代入草稿時的內容（＝「使用者什麼都沒寫」的樣子）
  const enabled = ref(false); // 資料還沒載入好之前，不追蹤、不攔截

  function snapshot() {
    const data = {};
    for (const name of toValue(fields)) data[name] = form[name];
    return JSON.stringify(data);
  }

  /**
   * 「如果現在離開，應該留下什麼」的快照：
   * 有草稿 → 草稿的內容；沒有草稿 → 初始狀態。
   * 以初始狀態為底再蓋上草稿的值，欄位順序才會跟 snapshot() 一樣，字串才比得起來。
   */
  function referenceSnapshot() {
    const base = JSON.parse(pristine);
    const draft = load();
    savedAt.value = draft?.savedAt ?? null; // 草稿被外部刪掉的話，「上次儲存」的提示也跟著消失
    if (draft) {
      for (const name of toValue(fields)) {
        if (draft[name] !== undefined && draft[name] !== null) base[name] = draft[name];
      }
    }
    return JSON.stringify(base);
  }

  /** 現在離開會不會弄丟東西。在「要離開的那一刻」才呼叫，不做成 computed（localStorage 不是響應式的）。 */
  function isDirty() {
    return enabled.value && snapshot() !== referenceSnapshot();
  }

  // ── localStorage ──
  //  每一個存取都包 try：無痕模式的某些設定、或空間滿了，localStorage 會直接丟例外。
  //  草稿是「方便」不是「必要」，壞掉就當作沒有草稿，不能讓整頁跟著壞。
  function load() {
    try {
      const raw = localStorage.getItem(toValue(key));
      return raw ? JSON.parse(raw) : null;
    } catch (e) {
      console.warn("[useDraft] 草稿讀取失敗，已忽略：", e);
      return null;
    }
  }

  function write() {
    const data = { savedAt: new Date().toISOString() };
    for (const name of toValue(fields)) data[name] = form[name];
    try {
      localStorage.setItem(toValue(key), JSON.stringify(data));
      return data;
    } catch (e) {
      console.warn("[useDraft] 草稿儲存失敗：", e);
      return null;
    }
  }

  /**
   * 資料載入好之後由頁面呼叫一次：有草稿就代入，然後開始追蹤。
   * 只代入目前允許的欄位——例如未登入時「匿名」是鎖死的，
   * 就算草稿裡存了 false 也不會蓋過去。
   */
  function start() {
    pristine = snapshot(); // 先記下「什麼都沒寫」的樣子，再代入草稿
    const draft = load();
    if (draft) {
      for (const name of toValue(fields)) {
        if (draft[name] !== undefined && draft[name] !== null) form[name] = draft[name];
      }
      savedAt.value = draft.savedAt ?? null;
    }
    enabled.value = true;
  }

  /** 手動儲存。回傳 true＝真的存了；false＝使用者取消覆蓋或儲存失敗。 */
  async function save(skipOverwriteConfirm = false) {
    if (!skipOverwriteConfirm) {
      const existing = load();
      if (existing?.savedAt) {
        const when = formatTime(existing.savedAt);
        const ok = confirmOverwrite
          ? await confirmOverwrite(when)
          : window.confirm(`已經有一份 ${when} 儲存的草稿。\n要用現在的內容覆蓋它嗎？`);
        if (!ok) return false;
      }
    }
    const data = write();
    if (!data) return false;
    savedAt.value = data.savedAt;
    return true;
  }

  /** 送出成功之後呼叫：清掉草稿，也不再攔截離開。 */
  function discard() {
    try {
      localStorage.removeItem(toValue(key));
    } catch {
      /* 忽略 */
    }
    savedAt.value = null;
    enabled.value = false;
  }

  // ── 站內換頁（RouterLink、router.push、瀏覽器上一頁）──
  //  Razor 時期只攔得到「頁面裡的 <a>」，瀏覽器上一頁只能給兩選一。
  //  SPA 裡上一頁也是 vue-router 在處理，所以也能給三選一了。
  onBeforeRouteLeave(async () => {
    if (!isDirty()) return true;

    const choice = confirmLeave
      ? await confirmLeave()
      : window.confirm("你填寫的內容還沒有儲存，確定要離開嗎？")
        ? "discard"
        : "cancel";

    // 三種都明確列出來。對話框被 Esc 或點外面關掉時 choice 是 undefined，當成「留在這頁」
    if (choice === "save") return await save(true); // 使用者已經在對話框表態過，不再問覆蓋；存失敗就不走
    if (choice === "discard") return true;
    return false;
  });

  // ── 關分頁、F5、在網址列打別的網址 ──
  //  這些 vue-router 攔不到，只能用瀏覽器內建的對話框（文字不能自訂，只有兩個按鈕）。
  function onBeforeUnload(e) {
    if (!isDirty()) return;
    e.preventDefault();
    e.returnValue = ""; // 舊瀏覽器要這行才會跳對話框，字串內容不會顯示
  }
  onMounted(() => window.addEventListener("beforeunload", onBeforeUnload));
  onBeforeUnmount(() => window.removeEventListener("beforeunload", onBeforeUnload));

  const savedAtText = computed(() =>
    savedAt.value ? `上次儲存：${formatTime(savedAt.value)}` : "",
  );

  return { start, save, discard, isDirty, savedAtText };
}

// 存的是 ISO（好比較先後），顯示時才轉成台灣習慣的格式
function formatTime(iso) {
  try {
    return new Date(iso).toLocaleString("zh-TW", { hour12: false });
  } catch {
    return iso;
  }
}
