/* ============================================================
   areview-queue.js — 館方評論清單
   放置路徑：wwwroot/js/areview-queue.js
   相依：axios、Bootstrap 5（Collapse、Modal）

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
      const FILTER_KEYS = ['tab', 'source', 'range', 'field', 'keyword', 'grouped'];

      /* ── 共用工具 ─────────────────────────────────────── */

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
                  return { tab: 'unread', source: 'all', range: 'month', field: 'content', keyword: '', grouped: 'false' };
            }
      }

      /* 篩選連結（tab、來源）上帶著完整條件，直接從 href 讀，
         連結本身就是唯一的事實來源，不用在 JS 裡重組一次。 */
      function filterFromHref(href) {
            const u = new URL(href, window.location.origin);
            const f = currentFilter();
            FILTER_KEYS.forEach(function (k) {
                  const v = u.searchParams.get(k);
                  if (v !== null) f[k] = v;
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

                    root.innerHTML = res.data;
                    highlight();
                    syncUrl(filter);

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
            });
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
    const spamReason = document.getElementById('spamReason');
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

          // 5. 打開標記垃圾的 modal
        const spamBtn = e.target.closest('.js-spam-open');
        if (spamBtn) {
            spamUrl = spamBtn.dataset.url;
            spamReason.value = '';
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

      /* ── 字數計數 ─────────────────────────────────────── */

    root.addEventListener('input', function (e) {
        if (!e.target.matches('.js-reply-text')) return;
        const form = e.target.closest('form');
        const len = e.target.value.length;
        const counter = form.querySelector('.js-count');
        counter.textContent = len;
        // 罐頭回覆是程式插入的，maxlength 擋不住，超過時要看得出來
        counter.parentElement.classList.toggle('text-danger', len > e.target.maxLength);
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
        if (spamReason.value === '') {
            spamError.textContent = '請選擇理由';
            return;
        }
        spamConfirm.disabled = true;

        post(spamUrl, { reason: spamReason.value })
            .then(function (res) {
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
