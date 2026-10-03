/* ============================================================
   reservation-list.js — 預約列表頁
   放置路徑：wwwroot/js/reservation-list.js
   相依：axios、Bootstrap 5（Modal）

   架構比照 areview-queue.js（評論管理佇列）：
   ⚠️ 表格（含篩選列、取消彈窗）會被整塊換掉（重新載入 Partial），
      所以事件一律掛在外層的 #reservationListRoot 上，再判斷實際點到/
      送出的是誰（事件委派）。如果直接掛在表格裡的按鈕或表單上，
      換掉之後新的元素就沒有事件了。
   ============================================================ */

(function () {
    'use strict';

    const root = document.getElementById('reservationListRoot');
    if (!root) return;

    const listUrl = root.dataset.listUrl;
    const flashBox = document.getElementById('reservationFlash');

    /* ── 共用工具 ─────────────────────────────────────── */

    function flash(message, type) {
        if (!flashBox) return;

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

    /* 伺服器有回 JSON 的 message 就用它；
       沒有 response 代表是別的原因（例如網路斷線），用瀏覽器給的錯誤訊息。 */
    function errorMessage(err) {
        const data = err.response && err.response.data;
        if (data && typeof data === 'object' && data.message) return data.message;
        if (err && !err.response && err.message) return err.message;
        return '操作失敗，請稍後再試';
    }

    /* ── 目前的篩選條件 ──────────────────────────────────
       存在 #reservationState 的 data-* 上，由後端每次重新渲染表格時一起吐出來。 */

    function stateEl() {
        return root.querySelector('#reservationState');
    }

    function currentFilter() {
        const st = stateEl();
        if (!st) return {};

        return {
            keyword: st.dataset.keyword || '',
            dateFrom: st.dataset.dateFrom || '',
            dateTo: st.dataset.dateTo || '',
            reservationStatus: st.dataset.reservationStatus || '',
            paymentStatus: st.dataset.paymentStatus || ''
        };
    }

    /* 從篩選表單目前畫面上的輸入值組出篩選條件，用在「查詢」按鈕被按下的當下——
       不能用 currentFilter()（那是後端上一次收到的值），
       否則使用者剛打的字會被舊資料蓋掉。 */
    function filterFromForm(form) {
        const data = new FormData(form);
        return {
            keyword: data.get('keyword') || '',
            dateFrom: data.get('dateFrom') || '',
            dateTo: data.get('dateTo') || '',
            reservationStatus: data.get('reservationStatus') || '',
            paymentStatus: data.get('paymentStatus') || ''
        };
    }

    function syncUrl(filter) {
        const url = new URL(window.location.href);
        Object.keys(filter).forEach(function (key) {
            const value = filter[key];
            if (value === '' || value === null || value === undefined) url.searchParams.delete(key);
            else url.searchParams.set(key, value);
        });
        // 用 replaceState 不用 pushState：不增加瀏覽紀錄，
        // 按瀏覽器上一頁會直接離開這一頁，不必處理「上一頁要回到哪個篩選」。
        history.replaceState(null, '', url);
    }

    /* ── 重新載入表格 ─────────────────────────────────── */

    function loadList(filter) {
        return axios.get(listUrl, { params: filter, responseType: 'text' })
            .then(function (res) {
                // 登入逾時的陷阱：Cookie 過期後伺服器會把請求導向登入頁，
                // axios 會自動跟著導向，最後拿回「200 + 登入頁的 HTML」。
                // 用「裡面有沒有 reservationState」判斷是不是真的拿到表格。
                if (typeof res.data !== 'string' || res.data.indexOf('id="reservationState"') === -1) {
                    flash('登入可能已經逾時，請重新整理頁面後再試一次', 'warning');
                    return;
                }

                const wrapper = document.getElementById('reservationTableWrapper');
                wrapper.innerHTML = res.data;
                syncUrl(filter);
            })
            .catch(function (err) {
                flash(errorMessage(err), 'danger');
            });
    }

    /* ── 表單送出（全部委派在 root 上）──────────────────── */

    root.addEventListener('submit', function (e) {

        // 1. 篩選表單：查詢
        if (e.target.matches('#reservationFilterForm')) {
            e.preventDefault();
            loadList(filterFromForm(e.target));
            return;
        }

        // 2. 其餘表單都是取消彈窗的表單（_TerminateReservationModal 產生的）
        const form = e.target;
        if (!form.matches('form')) return;
        e.preventDefault();

        const submitBtn = form.querySelector('[type="submit"]');
        if (submitBtn) submitBtn.disabled = true;

        // new FormData(form) 會包含表單裡自動產生的防偽 token
        axios.post(form.action, new FormData(form))
            .then(function (res) {
                const modalEl = form.closest('.modal');
                if (modalEl) {
                    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
                    modal.hide();
                }

                flash(res.data.message || '操作已完成', res.data.success ? 'success' : 'danger');
                return loadList(currentFilter());
            })
            .catch(function (err) {
                flash(errorMessage(err), 'danger');
            })
            .finally(function () {
                if (submitBtn) submitBtn.disabled = false;
            });
    });

    /* ── 點擊：重置 ──────────────────────────────────────
       重置 = 全部篩選條件清空，不用另外跟後端要預設值。 */

    root.addEventListener('click', function (e) {
        if (!e.target.closest('#reservationSearchReset')) return;
        loadList({});
    });

})();
