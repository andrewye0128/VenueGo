/* ============================================================
   areview-queue.js — 館方評論清單
   放置路徑：wwwroot/js/areview-queue.js
   相依：axios、Bootstrap 5（Collapse、Modal）

   ⚠️ 清單會被整塊換掉（重新載入 Partial），所以事件一律掛在
      外層的 #queueRoot 上，再判斷實際點到的是誰（事件委派）。
      如果直接掛在清單裡的按鈕上，換掉之後新的按鈕就沒有事件了。
   ============================================================ */

(function () {
    'use strict';

    const root = document.getElementById('queueRoot');
    if (!root) return;

    const listUrl = root.dataset.listUrl;
    const flashBox = document.getElementById('queueFlash');

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
        return axios.post(url, body);
    }

    /* axios 遇到 4xx / 5xx 會進 catch。
       伺服器有回 ApiResult 就用它的 message，否則給一句通用的。 */
    function errorMessage(err) {
        const data = err.response && err.response.data;
        if (data && data.message) return data.message;
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

    function currentFilter() {
        const state = root.querySelector('#queueState');
        return { tab: state.dataset.tab, source: state.dataset.source };
    }

    /* 重新載入清單（Partial），並把網址列換成目前的篩選條件，
       這樣按 F5 或複製網址給同事，看到的會是同一個清單。
       用 replaceState 而不是 pushState：不增加瀏覽紀錄，
       按瀏覽器上一頁會直接離開這一頁，不必另外處理「上一頁要回到哪個篩選」。 */
    function loadList(filter) {
        return axios.get(listUrl, { params: filter, responseType: 'text' })
            .then(function (res) {
                root.innerHTML = res.data;

                const url = new URL(window.location.href);
                url.searchParams.set('tab', filter.tab);
                url.searchParams.set('source', filter.source);
                history.replaceState(null, '', url);
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

    /* ── 點擊（全部委派在 root 上）──────────────────────── */

    let spamUrl = null;
    const spamModalEl = document.getElementById('spamModal');
    const spamModal = bootstrap.Modal.getOrCreateInstance(spamModalEl);
    const spamReason = document.getElementById('spamReason');
    const spamError = document.getElementById('spamError');
    const spamConfirm = document.getElementById('spamConfirm');

    root.addEventListener('click', function (e) {

        // 1. 篩選連結、重新整理
        const link = e.target.closest('.js-queue-link');
        if (link) {
            e.preventDefault();
            const u = new URL(link.href);
            loadList({
                tab: u.searchParams.get('tab'),
                source: u.searchParams.get('source')
            });
            return;
        }

        // 2. 置頂：送出「要變成什麼」，成功後重新載入（排序會變）
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

        // 3. 罐頭回覆：點了才插入，已有文字就接在後面
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

        // 4. 打開標記垃圾的 modal
        const spamBtn = e.target.closest('.js-spam-open');
        if (spamBtn) {
            spamUrl = spamBtn.dataset.url;
            spamReason.value = '';
            spamError.textContent = '';
            spamModal.show();
        }
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

})();
