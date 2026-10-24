/* ============================================================
   areview-queue.js — 館方評論清單
   放置路徑：wwwroot/js/areview-queue.js
   相依：axios、Bootstrap 5（Collapse、Modal）

   10/24 沒送出的回覆不會因為清單重新載入而消失（說明在「沒送出的回覆」那一段）。

   ⚠️ 清單會被整塊換掉（重新載入 Partial），所以事件一律掛在
      外層的 #queueRoot 上，再判斷實際點到的是誰（事件委派）。
      如果直接掛在清單裡的按鈕上，換掉之後新的按鈕就沒有事件了。

      搜尋列、時間範圍下拉、分組開關也在 Partial 裡面，
      所以它們的 change / keydown 同樣要委派，不能直接掛。
   ============================================================ */

(function () {
    'use strict';

    const root = document.getElementById('queueRoot');
    if (!root) return;

    const listUrl = root.dataset.listUrl;
    const flashBox = document.getElementById('queueFlash');

      // 所有會進網址、也會送給後端的條件
      // 9/29：多了運動類型與場地。dataset 會把 data-sport-type-id 轉成 sportTypeId。
      // 10/23：多了 page（分頁）。換任何篩選條件都回到第 1 頁：篩選連結的網址本來就不帶 page，
      //        下拉選單、搜尋那幾個地方則在送出前把 page 清掉（見 uiFilter）。
      //        處理完一則評論（置頂、回覆、標記垃圾）後重新載入時用 currentFilter，會留在同一頁。
      const FILTER_KEYS = ['tab', 'source', 'range', 'field', 'keyword', 'grouped', 'sportTypeId', 'venueId', 'page'];

      /* ── 共用工具 ─────────────────────────────────────── */

    /* AI 草稿的重複產生上限。
       這是「防手滑」不是「防惡意」——會按到這顆按鈕的人本來就得先有員工帳號，
       真正的成本控制在後端。這裡只是避免有人連點十次把額度燒光。

       ⚠️ Map 存在模組層級，清單被整塊換掉（切分頁、重新篩選）時不會清空，
          這是刻意的：如果切個分頁再回來就能重刷，這個限制等於不存在。
          重新整理整頁才會歸零，那不值得為它寫進資料庫。 */
    const AI_DRAFT_LIMIT = 3;
    const aiDraftCount = new Map();

      /* ── 10/24：沒送出的回覆 ─────────────────────────────
         換頁、換篩選、處理完別則評論之後，清單會整塊換掉，回覆框裡打到一半的字原本會跟著不見，也沒有任何提醒。
         - 打字時把內容記在 replyDrafts（評論編號 → 內容）。清單換掉之後，那則評論還在畫面上就把內容填回去，
           摘要列加上「未送出的回覆」標籤；原本展開的那一則也重新展開。
         - 那則評論不在新的這一頁（例如換頁了）：內容繼續留著，回到那則評論就會填回去；離開時提示一次。
         - 送出成功、標記垃圾成功才刪掉。那則評論已經被別人處理掉（畫面上有它，但沒有回覆框）也刪掉。
         - 重新整理整頁、關掉分頁的話記憶體裡的內容會不見，所以還有沒送出的回覆時，用瀏覽器內建的「確定要離開嗎？」攔一下。
           （這個對話框的文字瀏覽器不讓網頁改，固定是瀏覽器自己的說法。） */
      const replyDrafts = new Map();   // reviewId → { text, ai }（ai：「這段草稿由 AI 產生」的提示是不是亮著）

      // 評論編號從網址最後一段拿：/AReview/Reply/123、/AReview/MarkSpam/123
      function reviewIdOfUrl(url) {
            const m = new URL(url, location.href).pathname.match(/(\d+)\/?$/);
            return m ? m[1] : null;
      }
      function reviewIdOfForm(form) {
            return reviewIdOfUrl(form.getAttribute('action') || form.action);
      }

      function draftBadge(form, show) {
            const item = form.closest('.accordion-item');
            const row = item && item.querySelector('.accordion-button .d-flex');
            if (!row) return;
            let badge = row.querySelector('.js-draft-badge');
            if (show && !badge) {
                  badge = document.createElement('span');
                  badge.className = 'badge rounded-pill text-bg-warning js-draft-badge';
                  badge.innerHTML = '<i class="bi bi-pencil"></i> 未送出的回覆';
                  row.appendChild(badge);
            } else if (!show && badge) {
                  badge.remove();
            }
      }

      function rememberDraft(textarea) {
            const form = textarea.closest('form');
            const id = form && reviewIdOfForm(form);
            if (!id) return;
            if (textarea.value.trim()) {
                  const note = form.querySelector('.js-ai-note');
                  replyDrafts.set(id, { text: textarea.value, ai: !!note && !note.classList.contains('d-none') });
            } else {
                  replyDrafts.delete(id);
                  draftBadge(form, false);
            }
      }

      // 清單換掉之前：記下哪一則是展開的、這一頁有哪些沒送出的回覆
      function captureBeforeRender() {
            const open = root.querySelector('.accordion-collapse.show');
            const onPage = [];
            root.querySelectorAll('.js-reply-form').forEach(function (form) {
                  const id = reviewIdOfForm(form);
                  if (id && replyDrafts.has(id)) onPage.push(id);
            });
            return { openId: open ? open.id : null, onPage: onPage };
      }

      // 清單換掉之後：填回去、重新展開、提示不在這一頁的
      function restoreAfterRender(before) {
            const restored = new Set();
            root.querySelectorAll('.js-reply-form').forEach(function (form) {
                  const id = reviewIdOfForm(form);
                  const draft = id && replyDrafts.get(id);
                  if (!draft) return;
                  const textarea = form.querySelector('.js-reply-text');
                  textarea.value = draft.text;
                  const note = form.querySelector('.js-ai-note');
                  if (note && draft.ai) note.classList.remove('d-none');
                  textarea.dispatchEvent(new Event('input', { bubbles: true }));   // 字數、自動長高跟著更新
                  draftBadge(form, true);
                  restored.add(id);
            });

            // 畫面上有這則評論、卻沒有回覆框：已經被處理掉了（例如別的員工回覆了），草稿留著也送不出去
            root.querySelectorAll('.accordion-collapse[id^="rev-"]').forEach(function (el) {
                  const id = el.id.slice(4);
                  if (replyDrafts.has(id) && !el.querySelector('.js-reply-form')) replyDrafts.delete(id);
            });

            // 原本展開的那一則重新展開（直接加 class，不播動畫，也不會再送一次「已閱覽」）
            if (before.openId) {
                  const collapse = document.getElementById(before.openId);
                  if (collapse && root.contains(collapse) && !collapse.classList.contains('show')) {
                        collapse.classList.add('show');
                        const btn = root.querySelector('[data-bs-target="#' + before.openId + '"]');
                        if (btn) {
                              btn.classList.remove('collapsed');
                              btn.setAttribute('aria-expanded', 'true');
                        }
                  }
            }

            const gone = before.onPage.filter(function (id) { return !restored.has(id) && replyDrafts.has(id); });
            if (gone.length) {
                  flash('有 ' + gone.length + ' 則還沒送出的回覆不在這一頁。內容已經幫你留著，回到那則評論就會自動填回。', 'warning');
            }
      }

      window.addEventListener('beforeunload', function (e) {
            if (replyDrafts.size === 0) return;
            e.preventDefault();
            e.returnValue = '';   // 舊版瀏覽器要設這個才會跳出確認
      });

      function pageToken() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    /* 送 POST。
       用「表單格式」而不是 JSON：Action 的參數是 int id、bool isPinned 這種單一值，
       表單格式送上去就能直接繫結；送 JSON 的話要另外在 C# 端加 [FromBody] 和一個類別。
       axios 看到 URLSearchParams 會自動設定對應的 Content-Type。 */
    function post(url, data) {
        const body = new URLSearchParams(data || {});
        if (!body.has('__RequestVerificationToken')) {
            body.append('__RequestVerificationToken', pageToken());
        }
          return axios.post(url, body).then(assertJson);
    }

      /* ⚠️ 登入逾時的陷阱：
         Cookie 過期之後，伺服器會把請求 302 導向 /Account/Login，
         而 axios 會自動跟著導向，最後拿回「200 + 登入頁的 HTML」。
         這不會進 catch，會進 then——如果不擋，畫面會跳「回覆已送出」，
         但資料庫其實一個字都沒進去。失敗被當成成功比直接報錯糟得多。
  
         伺服器正常回的一定是 JSON 物件，登入頁則是字串，用這點分辨。 */
      function assertJson(res) {
            if (!res.data || typeof res.data !== 'object') {
                  throw new Error('登入可能已經逾時，請重新整理頁面後再試一次');
            }
            return res;
      }

      /* axios 遇到 4xx / 5xx 會進 catch。
         伺服器有回 ApiResult 就用它的 message；
         沒有 response 代表是上面 assertJson 自己丟的，用它的訊息。 */
      function errorMessage(err) {
            const data = err.response && err.response.data;
            if (data && typeof data === 'object' && data.message) return data.message;
            if (err && !err.response && err.message) return err.message;
            return '操作失敗，請稍後再試';
      }

    function flash(message, type) {
        const div = document.createElement('div');
        div.className = 'alert alert-' + (type || 'success') + ' alert-dismissible fade show';
        div.textContent = message;     // 用 textContent，訊息裡有特殊字元也不會被當成 HTML

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'btn-close';
        close.setAttribute('data-bs-dismiss', 'alert');
        close.setAttribute('aria-label', '關閉');
        div.appendChild(close);

        flashBox.replaceChildren(div);
        setTimeout(function () { div.remove(); }, 4000);
    }

      /* ── 目前的條件 ────────────────────────────────────
         全部存在 #queueState 的 data-* 上，由後端每次一起吐出來。 */

      function stateEl() {
            return root.querySelector('#queueState');
      }

      /* 後端認定的條件。處理完一則評論之後重新載入清單用這個——
         那種情境不該把使用者還沒按 Enter 的半成品關鍵字送出去。 */
      function currentFilter() {
            const st = stateEl();
            const f = {};
            FILTER_KEYS.forEach(function (k) {
                  f[k] = st ? (st.dataset[k] || '') : '';
            });
            return f;
      }

      /* 使用者正在操作篩選列時用這個：關鍵字改讀「畫面上現在打的字」。
  
         ⚠️ 這兩個要分開，否則會出現這個 bug：
            使用者把搜尋框清空但沒按 Enter，接著切換搜尋屬性 →
            切換時如果讀 dataset.keyword（後端上次收到的值），
            清空的動作就被蓋掉，畫面上會冒出剛剛刪掉的字。
            眼前看得到的輸入框才是使用者的真實意圖。 */
      function uiFilter() {
            const f = currentFilter();
            const box = root.querySelector('#searchKeyword');
            if (box) f.keyword = box.value;
            f.page = '';   // 10/23：換篩選條件就回到第 1 頁（第 5 頁的位置對新條件沒有意義）
            return f;
      }

    /* 「重置」要回到的值。由後端吐在 data-defaults 上，
   前端不自己記一份，後端改了預設值這裡就跟著改，不會走鐘。 */
      function defaultFilter() {
            const st = stateEl();
            try {
                  return JSON.parse(st.dataset.defaults);
            } catch (e) {
                  // 萬一讀不到也要有得用，不能讓「重置」整個壞掉
                  return { tab: 'unread', source: 'all', range: 'month', field: 'content', keyword: '', grouped: 'false', sportTypeId: '', venueId: '', page: '' };
            }
      }

      /* 篩選連結（tab、來源、場地）上帶著完整條件，直接從 href 讀，
         連結本身就是唯一的事實來源，不用在 JS 裡重組一次。

         9/29 改：網址上「沒有」這個條件，就當作空值，不再沿用目前的值。
         原因是「取消場地篩選」的連結是把 venueId 拿掉（後端產生網址時，空值不會寫進去），
         如果沿用目前的值，場地就永遠取消不掉。
         其他條件不受影響：每個連結本來就帶著全部條件（見 _QueueList 的 CurrentRoute）。 */
      function filterFromHref(href) {
            const u = new URL(href, window.location.origin);
            const f = {};
            FILTER_KEYS.forEach(function (k) {
                  const v = u.searchParams.get(k);
                  f[k] = v !== null ? v : '';
            });
            return f;
      }

      function syncUrl(filter) {
            const url = new URL(window.location.href);
            FILTER_KEYS.forEach(function (k) {
                  const v = filter[k];
                  if (v === '' || v === null || v === undefined) url.searchParams.delete(k);
                  else url.searchParams.set(k, v);
            });
            // 用 replaceState 不用 pushState：不增加瀏覽紀錄，
            // 按瀏覽器上一頁會直接離開這一頁，不必處理「上一頁要回到哪個篩選」。
            history.replaceState(null, '', url);
      }

      /* ── 重新載入清單 ─────────────────────────────────── */
      /* 重新載入清單（Partial），並把網址列換成目前的篩選條件，
       這樣按 F5 或複製網址給同事，看到的會是同一個清單。
       用 replaceState 而不是 pushState：不增加瀏覽紀錄，
       按瀏覽器上一頁會直接離開這一頁，不必另外處理「上一頁要回到哪個篩選」。 */
      function loadList(filter, options) {
            options = options || {};

        return axios.get(listUrl, { params: filter, responseType: 'text' })
              .then(function (res) {
                    // 同樣的登入逾時陷阱：這裡拿回來的本來就是 HTML，
                    // 所以改用「裡面有沒有 queueState」來判斷是不是真的清單。
                    if (typeof res.data !== 'string' || res.data.indexOf('id="queueState"') === -1) {
                          flash('登入可能已經逾時，請重新整理頁面後再試一次', 'warning');
                          return;
                    }

                    const draftsBefore = captureBeforeRender();   // 10/24：沒送出的回覆
                    root.innerHTML = res.data;
                    restoreAfterRender(draftsBefore);
                    highlight();
                    syncUrl(filter);
                    // 10/23：換頁之後，如果清單開頭已經捲到畫面外（例如在最後一則按了下一頁），捲回清單開頭
                    if (options.revealList) revealListTop();

                    // 清單整塊被換掉，剛才操作的那個控制項也跟著不見了，
                    // 焦點會掉到 body。把焦點還給它，鍵盤操作才不會斷掉。
                    if (options.focusId) {
                          const el = root.querySelector('#' + options.focusId);
                          if (el) {
                                el.focus();
                                if (el.tagName === 'INPUT') {
                                      // 游標移到最後：重新 render 之後游標預設會停在最前面，
                                      // 想接著改關鍵字的人要先按 End，很煩。
                                      const v = el.value;
                                      el.value = '';
                                      el.value = v;
                                }
                          }
                    }
              })
            .catch(function (err) {
                flash(errorMessage(err), 'danger');
            })
                  // 10/23：告訴 areview-pager.js「載入結束了」（成功或失敗都要），它才會把快轉時預覽的頁碼清掉
                  .finally(function () {
                        document.dispatchEvent(new CustomEvent('queue:rendered'));
                  });
      }

      /* ── 換頁（10/23）──────────────────────────────────
         換頁 Bar、Page Up／Page Down、長按快轉都在 areview-pager.js，
         它決定好要去第幾頁之後發出 queue:go-page，由這裡用目前的條件載入那一頁。 */
      document.addEventListener('queue:go-page', function (e) {
            const f = currentFilter();
            f.page = String(e.detail.page);
            loadList(f, { revealList: true });
      });

            // 頁首（.admin-header）是黏在上方的，要扣掉它的高度，清單開頭才不會被蓋住。
            // 清單開頭還看得到時不捲：例如人在篩選列上方，畫面不該突然往下跳。
            function revealListTop() {
                  const el = root.querySelector('#queueListArea');
                  if (!el) return;
                  const header = document.querySelector('.admin-header');
                  const offset = (header ? header.offsetHeight : 0) + 12;
                  const top = el.getBoundingClientRect().top;
                  if (top >= offset) return;
                  window.scrollTo({ top: window.scrollY + top - offset });
            }

    function adjustCount(name, delta) {
        const badge = root.querySelector('[data-count-for="' + name + '"]');
        if (!badge) return;
        badge.textContent = Math.max(0, Number(badge.textContent) + delta);
      }

      /* ── 關鍵字標註 ───────────────────────────────────────
         在前端做，不在 Razor 做。
         Razor 那邊要自己把內容 HtmlEncode 再插 <span> 然後 Html.Raw，
         只要漏一步就是 XSS 破口——評論內容是顧客打的，不可信。
         這裡全程用 textContent 和 createTextNode，任何字元都不會被當成 HTML。
  
         另外用 indexOf 逐字找，不用正規表示式：關鍵字裡如果有 (、*、[
         這種字元，組成 RegExp 會直接壞掉或找錯。 */

      function findRanges(text, words) {
            const lower = text.toLowerCase();
            const hits = [];

            words.forEach(function (w) {
                  const needle = w.toLowerCase();
                  if (!needle) return;
                  let from = 0;
                  let idx = lower.indexOf(needle, from);
                  while (idx !== -1) {
                        hits.push({ start: idx, end: idx + needle.length });
                        from = idx + needle.length;
                        idx = lower.indexOf(needle, from);
                  }
            });

            if (!hits.length) return hits;

            hits.sort(function (a, b) { return a.start - b.start || b.end - a.end; });

            // 兩個關鍵字重疊時（例如搜「網球」和「球場」，文字是「網球場」），
            // 不合併的話會切出錯亂的片段，所以重疊的區段合成一段。
            const merged = [hits[0]];
            for (let i = 1; i < hits.length; i++) {
                  const last = merged[merged.length - 1];
                  if (hits[i].start < last.end) {
                        last.end = Math.max(last.end, hits[i].end);
                  } else {
                        merged.push(hits[i]);
                  }
            }
            return merged;
      }

      function markInside(el, words) {
            const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
            const targets = [];
            let node = walker.nextNode();
            while (node) {
                  if (node.nodeValue && node.nodeValue.trim()) targets.push(node);
                  node = walker.nextNode();
            }

            targets.forEach(function (textNode) {
                  const text = textNode.nodeValue;
                  const ranges = findRanges(text, words);
                  if (!ranges.length) return;

                  const frag = document.createDocumentFragment();
                  let pos = 0;
                  ranges.forEach(function (r) {
                        if (r.start > pos) {
                              frag.appendChild(document.createTextNode(text.slice(pos, r.start)));
                        }
                        const mk = document.createElement('mark');
                        mk.textContent = text.slice(r.start, r.end);
                        frag.appendChild(mk);
                        pos = r.end;
                  });
                  if (pos < text.length) {
                        frag.appendChild(document.createTextNode(text.slice(pos)));
                  }

                  textNode.parentNode.replaceChild(frag, textNode);
            });
      }

      function highlight() {
            const st = stateEl();
            if (!st) return;

            let words = [];
            try {
                  words = JSON.parse(st.dataset.keywords || '[]');
            } catch (e) {
                  words = [];
            }
            if (!words.length) return;

            // 長的先標：先標短的會把長的切開，結果變成一段一段的碎片
            words = words.slice().sort(function (a, b) { return b.length - a.length; });

            root.querySelectorAll('.js-hl').forEach(function (el) {
                  markInside(el, words);
            });
      }

      /* ── 點擊（全部委派在 root 上）──────────────────────── */

    let spamUrl = null;
    const spamModalEl = document.getElementById('spamModal');
    const spamModal = bootstrap.Modal.getOrCreateInstance(spamModalEl);
    // 9/29：理由改成單選清單，選中的那一個用 checked 找
    function checkedSpamReason() {
        const el = document.querySelector('input[name="spamReason"]:checked');
        return el ? el.value : '';
    }
    const spamError = document.getElementById('spamError');
    const spamConfirm = document.getElementById('spamConfirm');

    root.addEventListener('click', function (e) {

          // 1. 重置：把所有條件恢復成後端給的預設值
          if (e.target.closest('#searchReset')) {
                loadList(defaultFilter(), { focusId: 'searchKeyword' });
                return;
          }

          // 2. 篩選連結、重新整理
        const link = e.target.closest('.js-queue-link');
        if (link) {
            e.preventDefault();
                loadList(filterFromHref(link.href));
                return;
          }

          // 3. 置頂：送出「要變成什麼」，成功後重新載入（排序會變）
        const pin = e.target.closest('.js-pin');
        if (pin) {
            const willPin = pin.getAttribute('aria-pressed') !== 'true';
            pin.disabled = true;
            post(pin.dataset.url, { isPinned: willPin })
                .then(function () { return loadList(currentFilter()); })
                .catch(function (err) {
                    flash(errorMessage(err), 'danger');
                    pin.disabled = false;
                });
            return;
        }

          // 4. 罐頭回覆：點了才插入，已有文字就接在後面
        const canned = e.target.closest('.js-canned');
        if (canned) {
            const textarea = canned.closest('form').querySelector('.js-reply-text');
            textarea.value = textarea.value
                ? textarea.value + '\n' + canned.dataset.text
                : canned.dataset.text;
            // 程式設定 value 不會觸發 input 事件，補發一次讓字數計數器更新
            textarea.dispatchEvent(new Event('input', { bubbles: true }));
            textarea.focus();
            return;
        }

        // 4b. AI 回覆草稿：後端產生 → 填進輸入框 → 員工改完才送出
        //     ⚠️ 這支不會送出回覆，送出走的還是原本那條路。
        const aiBtn = e.target.closest('.js-ai-draft');
        if (aiBtn) {
            const form = aiBtn.closest('form');
            const textarea = form.querySelector('.js-reply-text');
            const errorBox = form.querySelector('.js-error');
            const note = form.querySelector('.js-ai-note');
            const id = aiBtn.dataset.reviewId;

            const used = aiDraftCount.get(id) || 0;
            if (used >= AI_DRAFT_LIMIT) {
                errorBox.textContent =
                    '這則已經產生過 ' + used + ' 次草稿了。再生一次多半還是差不多，自己寫會比較快。';
                return;
            }

            /* 已經打了字就先問一聲。
               ⚠️ 這裡跟罐頭回覆的行為刻意不同：罐頭回覆是「接在後面」，
                  因為它是一個句子；AI 草稿是一整則完整回覆，只能「取代」。
                  接在後面會變成兩則回覆黏在一起。 */
            if (textarea.value.trim() &&
                !window.confirm('要用 AI 草稿取代目前輸入的內容嗎？')) {
                return;
            }

            // ⚠️ 防連點：每一次點擊都在花錢，而且 LLM 慢起來可以好幾秒。
            //    按鈕沒有鎖住的話，員工會以為沒反應而連按。
            const label = aiBtn.innerHTML;
            aiBtn.disabled = true;
            aiBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> 產生中⋯';
            errorBox.textContent = '';

            // post() 已經自動帶防偽 token，也會做 assertJson（擋 302 假成功）
            post(aiBtn.dataset.url, { id: id })
                .then(function (res) {
                    // 後端回的是 ApiResult<string>：success / message / data
                    if (!res.data.success) {
                        throw new Error(res.data.message || 'AI 草稿產生失敗');
                    }

                    textarea.value = res.data.data;

                    // 程式設定 value 不會觸發 input 事件，
                    // 補發一次讓下面的字數計數器更新（跟罐頭回覆同一個道理）
                    textarea.dispatchEvent(new Event('input', { bubbles: true }));

                    textarea.focus();
                    textarea.setSelectionRange(textarea.value.length, textarea.value.length);

                    // 標記「這段是機器寫的」。這行提示是責任界線，不是裝飾。
                    if (note) note.classList.remove('d-none');

                    aiDraftCount.set(id, used + 1);
                })
                .catch(function (err) {
                    /* ⚠️ 失敗就是失敗，輸入框一個字都不要動。
                       員工可能已經打了一半，被一個失敗的請求清空是最糟的結果。
                       錯誤寫進表單自己的 .js-error，跟其他操作一致，不要用 alert。 */
                    errorBox.textContent = errorMessage(err);
                })
                .finally(function () {
                    aiBtn.disabled = false;
                    aiBtn.innerHTML = label;
                });
            return;
        }

          // 5. 打開標記垃圾的 modal
        const spamBtn = e.target.closest('.js-spam-open');
        if (spamBtn) {
            spamUrl = spamBtn.dataset.url;
            document.querySelectorAll('input[name="spamReason"]').forEach(function (r) { r.checked = false; });
            spamError.textContent = '';
            spamModal.show();
        }
    });

      /* ── 下拉選單與分組開關 ─────────────────────────────── */

      root.addEventListener('change', function (e) {
            // 用 uiFilter：使用者可能剛改過搜尋框卻還沒按 Enter，
            // 以畫面上的內容為準，不要用後端上次收到的舊值蓋掉。
            const f = uiFilter();

            if (e.target.matches('#rangeSelect')) {
                  f.range = e.target.value;
                  loadList(f, { focusId: 'rangeSelect' });
                  return;
            }

            if (e.target.matches('#searchField')) {
                  // 只換搜尋的欄位，關鍵字保留，直接用新欄位重搜一次
                  f.field = e.target.value;
                  loadList(f, { focusId: 'searchField' });
                  return;
            }

            if (e.target.matches('#groupToggle')) {
                  f.grouped = e.target.checked ? 'true' : 'false';
                  loadList(f, { focusId: 'groupToggle' });
                  return;
            }

            // 9/29：換運動類型時，場地篩選一併取消——
            // 從「羽球場 A1」換成「網球」，還留著羽球場的篩選只會得到空清單。
            // （9/29 舊說明）依訂單分組也關掉：選了運動類型就改成依場地分組（後端也會強制）。
            // v2：依訂單分組保留使用者原本的選擇——選了類型時後端會暫時停用，換回「全部」就自動恢復。
            if (e.target.matches('#sportTypeSelect')) {
                  f.sportTypeId = e.target.value;
                  f.venueId = '';
                  // 原本：f.grouped = 'false';
                  loadList(f, { focusId: 'sportTypeSelect' });
            }
      });

      /* ── 搜尋：按 Enter 才送 ───────────────────────────────
         不做邊打邊搜：每打一個字就打一次資料庫，而中文輸入法在
         選字的過程中也會一直觸發，會送出一堆沒意義的查詢。
  
         用原生的 x 清空搜尋框之後，要再按一次 Enter 才會重新查；
         想一次清乾淨的話按「重置」。 */
      root.addEventListener('keydown', function (e) {
            if (!e.target.matches('#searchKeyword')) return;
            if (e.key !== 'Enter') return;

            e.preventDefault();
            const f = uiFilter();
            loadList(f, { focusId: 'searchKeyword' });
      });

      /* ── 回覆框自動長高（昱，10/14）───────────────────────
       初始 3 行；內容寫到最後一行時就多長一行，永遠替員工留一行空白，最多 10 行，再多才出現捲軸。
       用 scrollHeight 量「內容實際佔幾行」，所以一句太長自動折行也算得到，不是只數換行符號。
       罐頭回覆、AI 草稿是程式填入的，那兩段程式都會補發 input 事件，所以一樣會長高。 */
      const REPLY_MIN_ROWS = 3;
      const REPLY_MAX_ROWS = 10;

      function autoGrowReply(textarea) {
            const style = getComputedStyle(textarea);
            const lineHeight = parseFloat(style.lineHeight) || parseFloat(style.fontSize) * 1.5;
            const padding = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
            const keepScroll = window.scrollY;

            // 先縮成 1 行，scrollHeight 才會是「內容」的高度，而不是框本身的高度
            textarea.rows = 1;
            const usedRows = Math.max(1, Math.round((textarea.scrollHeight - padding) / lineHeight));
            textarea.rows = Math.min(Math.max(usedRows + 1, REPLY_MIN_ROWS), REPLY_MAX_ROWS);

            // 暫時縮成 1 行時頁面可能往上跳，量完放回原本的捲動位置
            window.scrollTo(window.scrollX, keepScroll);
      }

      /* ── 字數計數 ─────────────────────────────────────── */

    root.addEventListener('input', function (e) {
          if (!e.target.matches('.js-reply-text')) return;
          autoGrowReply(e.target);
        const form = e.target.closest('form');
        const len = e.target.value.length;
        const counter = form.querySelector('.js-count');
        counter.textContent = len;
        // 罐頭回覆是程式插入的，maxlength 擋不住，超過時要看得出來
        counter.parentElement.classList.toggle('text-danger', len > e.target.maxLength);

          rememberDraft(e.target);   // 10/24：沒送出的回覆
    });

    /* ── 送出回覆 ─────────────────────────────────────── */

    root.addEventListener('submit', function (e) {
        const form = e.target.closest('.js-reply-form');
        if (!form) return;
        e.preventDefault();

        const text = form.querySelector('.js-reply-text').value.trim();
        const errorBox = form.querySelector('.js-error');
        errorBox.textContent = '';

        if (!text) {
            errorBox.textContent = '請輸入回覆內容';
            return;
        }
        if (!window.confirm('確定送出回覆嗎？送出後無法修改。')) return;

        const submitBtn = form.querySelector('[type="submit"]');
        submitBtn.disabled = true;

        // new FormData(form) 會包含表單裡自動產生的防偽 token
        post(form.action, new FormData(form))
              .then(function (res) {
                    replyDrafts.delete(reviewIdOfForm(form));   // 10/24：送出了，草稿不用留
                flash(res.data.message || '回覆已送出');
                return loadList(currentFilter());
            })
            .catch(function (err) {
                errorBox.textContent = errorMessage(err);
                submitBtn.disabled = false;
            });
    });

    /* ── 展開時記錄閱覽 ───────────────────────────────────
       show.bs.collapse 在「開始展開」時觸發。
       Bootstrap 的事件會往外層傳，所以掛在 root 上就收得到。 */

    root.addEventListener('show.bs.collapse', function (e) {
        const item = e.target.closest('.accordion-item');
        if (!item || item.dataset.read === '1') return;

        item.dataset.read = '1';    // 先標記，避免收合再展開時重送
        post(item.dataset.readUrl)
            .then(function (res) {
                const badge = item.querySelector('.js-read-badge');
                if (badge) badge.classList.remove('d-none');

                // data 為 true 代表這次才讀的；false 代表別人剛讀過，數字早就不對了
                if (res.data.data === true) {
                    adjustCount('unread', -1);
                    adjustCount('pending', +1);
                }
            })
            .catch(function (err) {
                item.dataset.read = '0';    // 失敗就讓下次展開再試
                flash('閱覽紀錄沒有存到：' + errorMessage(err), 'warning');
            });
    });

    /* ── 確認標記垃圾（modal 在 root 外面，直接掛）──────── */

    spamConfirm.addEventListener('click', function () {
        const reason = checkedSpamReason();
        if (reason === '') {
            spamError.textContent = '請選擇理由';
            return;
        }
        spamConfirm.disabled = true;

        post(spamUrl, { reason: reason })
              .then(function (res) {
                    replyDrafts.delete(reviewIdOfUrl(spamUrl));   // 10/24：標記成垃圾了，草稿不用留
                spamModal.hide();
                flash(res.data.message || '已標記為垃圾');
                return loadList(currentFilter());
            })
            .catch(function (err) {
                spamError.textContent = errorMessage(err);
            })
            .finally(function () {
                spamConfirm.disabled = false;
            });
    });

      /* ── 第一次進頁面 ────────────────────────────────────
         整頁是由伺服器 render 的，沒有經過 loadList，
         所以要在這裡補標註一次（例如從書籤或別人給的網址進來）。 */
      highlight();

})();

/* ───────────────────────────────────────────────────────────────────────
   後端要回的格式（跟專案既有的 ApiResult 一致，不要自己另外發明）

       成功： return Ok(ApiResult<string>.Ok(draft));
              → { success: true, message: null, errorCode: null, data: "草稿內容" }

       失敗： return StatusCode(502, ApiResultVM.Fail("AI 服務暫時無法使用，請稍後再試或自行撰寫"));
              → axios 遇到 5xx 會進 catch，errorMessage() 會取出 message

       未啟用：return StatusCode(503, ApiResultVM.Fail("AI 草稿功能未啟用"));
       已回覆：return StatusCode(409, ApiResultVM.Fail("這則評論已經回覆過了"));

   ⚠️ 不要把例外的 ex.Message 直接回給前端——裡面可能有金鑰或內部路徑。
      詳細錯誤記進 log，回給前端的是給人看的那一句。
   ─────────────────────────────────────────────────────────────────────── */
