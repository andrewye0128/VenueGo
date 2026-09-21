/* ============================================================
   draft-box.js — 表單草稿模組（顧客端與館方端共用）
   放置路徑：wwwroot/js/draft-box.js

   相依：Bootstrap 5（用它的 Modal）。專案已載入，不必額外引入。

   ------------------------------------------------------------
   顧客端與館方端的差別只有兩件事：localStorage 的 key，
   以及要存哪些欄位。兩者都是呼叫時用參數給，程式碼本身共用。

     顧客端：key = `review-draft:${token}`
             fields = 星等、內容、兩個提及、匿名、公開
     館方端：key = `reply-draft:${reviewId}:${employeeId}`
             fields = 回覆內容

   ⚠️ 館方端的 key 一定要含員工 ID，否則 B 員工會代入 A 寫到
      一半的草稿，改一下送出就變成自己的業績。

   ------------------------------------------------------------
   ⚠️ 一個瀏覽器層級的限制，先講清楚

   「跳出三個選項讓使用者選」這件事，分成兩種離開方式：

   (1) 頁面內的連結、按鈕（例如「返回前頁」超連結）
       → 完全做得到。本模組會攔下來，跳出三選一的 Bootstrap Modal：
         留在這頁 ／ 不儲存直接離開 ／ 儲存草稿並離開

   (2) 關閉分頁、按 F5、按瀏覽器的上一頁
       → 做不到。這裡只能觸發瀏覽器內建的 beforeunload 對話框，
         它固定只有「離開／留下」兩個按鈕，文字也不能自訂
         （這是所有現代瀏覽器的安全限制，不是寫法問題）。
         本模組在這種情況下只能盡量攔住使用者，讓他有機會
         回頭自己按儲存。

   若你希望第 (2) 種情況也不要遺失資料，可以把 saveOnUnload
   設成 true：關閉前自動存一次草稿。localStorage 是同步 API，
   在 beforeunload 裡來得及執行。代價是它會靜默覆蓋既有草稿，
   跟「覆蓋前先確認」的設計相衝，所以預設關閉。
   ============================================================ */

window.DraftBox = (function () {
    'use strict';

    var MODAL_ID = 'draftLeaveModal';
    var modalEl = null;
    var modalInstance = null;

    /* ── 三選一的 Modal（第一次用到時才建，不必貼 HTML）── */
    function ensureModal() {
        if (modalEl) return;

        var html =
            '<div class="modal fade" id="' + MODAL_ID + '" tabindex="-1" aria-hidden="true">' +
              '<div class="modal-dialog modal-dialog-centered">' +
                '<div class="modal-content">' +
                  '<div class="modal-header">' +
                    '<h5 class="modal-title"><i class="bi bi-exclamation-triangle me-2"></i>尚未儲存</h5>' +
                  '</div>' +
                  '<div class="modal-body">' +
                    '<p class="mb-0">你填寫的內容還沒有儲存，離開這一頁之後會消失。</p>' +
                  '</div>' +
                  '<div class="modal-footer">' +
                    '<button type="button" class="btn btn-outline-secondary" data-draft-choice="cancel">留在這頁</button>' +
                    '<button type="button" class="btn btn-outline-danger"    data-draft-choice="discard">不儲存，直接離開</button>' +
                    '<button type="button" class="btn btn-primary"           data-draft-choice="save">儲存草稿並離開</button>' +
                  '</div>' +
                '</div>' +
              '</div>' +
            '</div>';

        document.body.insertAdjacentHTML('beforeend', html);
        modalEl = document.getElementById(MODAL_ID);
        modalInstance = new bootstrap.Modal(modalEl);
    }

    /* ── 時間格式化 ────────────────────────────────────
       存進 localStorage 的是 ISO 字串（機器讀），
       顯示時才轉成台灣格式（人讀）。存已格式化的字串會有麻煩：
       日後想比較兩個草稿誰新，字串比不出來。 */
    function formatTime(iso) {
        try {
            return new Date(iso).toLocaleString('zh-TW', { hour12: false });
        } catch (e) {
            return iso;
        }
    }

    /* ── 讀寫單一欄位 ──────────────────────────────────
       依 input 的 type 決定怎麼取值，呼叫端不必描述欄位型別，
       只要給名字。 */
    function readField(form, name) {
        var sel = '[name="' + name + '"]';
        var first = form.querySelector(sel);
        if (!first) return null;

        if (first.type === 'radio') {
            var checked = form.querySelector(sel + ':checked');
            return checked ? checked.value : null;
        }
        if (first.type === 'checkbox') {
            // ⚠️ asp-for 產生 bool 的 checkbox 時，會額外產生一個同名的
            //    hidden（value="false"），用來讓「沒勾」也送得出 false。
            //    所以這裡要明確指定 type，不能拿 querySelector 的第一個。
            var box = form.querySelector('input[type="checkbox"]' + sel);
            return box ? box.checked : null;
        }
        return first.value;
    }

    function writeField(form, name, value) {
        if (value === null || value === undefined) return;
        var sel = '[name="' + name + '"]';
        var first = form.querySelector(sel);
        if (!first) return;

        if (first.type === 'radio') {
            var radio = form.querySelector(sel + '[value="' + value + '"]');
            if (radio) { radio.checked = true; fireEvents(radio); }
            return;
        }
        if (first.type === 'checkbox') {
            var box = form.querySelector('input[type="checkbox"]' + sel);
            if (box) { box.checked = !!value; fireEvents(box); }
            return;
        }
        first.value = value;
        fireEvents(first);
    }

    /* ── 補發事件 ──
       用程式碼設定 .value 或 .checked 不會觸發 input／change，
       所以字數計數器、textarea 自動撐高這類監聽器不會跟著更新。
       還原草稿後補發一次，任何掛在表單上的監聽器都會自動跟上，
       不必在本模組裡逐一登記要通知誰。 */
    function fireEvents(el) {
        el.dispatchEvent(new Event('input',  { bubbles: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));
    }

    /* ============================================================
       init：每個頁面呼叫一次
       ============================================================ */
    function init(options) {
        var form = typeof options.form === 'string'
                 ? document.querySelector(options.form)
                 : options.form;

        if (!form) {
            console.warn('[DraftBox] 找不到表單：', options.form);
            return null;
        }

        var key            = options.key;
        var fields         = options.fields || [];
        var saveBtn        = options.saveButton ? document.querySelector(options.saveButton) : null;
        var timeHint       = options.timeHint   ? document.querySelector(options.timeHint)   : null;
        var confirmOverwrite = options.confirmOverwrite !== false;   // 預設開啟
        var saveOnUnload   = options.saveOnUnload === true;          // 預設關閉

        var dirty = false;

        /* ── 存取 localStorage ── */
        function load() {
            try {
                var raw = localStorage.getItem(key);
                return raw ? JSON.parse(raw) : null;
            } catch (e) {
                console.warn('[DraftBox] 草稿解析失敗，已忽略：', e);
                return null;
            }
        }

        function write() {
            var data = { savedAt: new Date().toISOString() };
            fields.forEach(function (name) {
                data[name] = readField(form, name);
            });
            try {
                localStorage.setItem(key, JSON.stringify(data));
            } catch (e) {
                // 空間滿了或瀏覽器停用 localStorage（無痕模式的某些設定）
                console.warn('[DraftBox] 草稿儲存失敗：', e);
                return null;
            }
            return data;
        }

        function clear() {
            try { localStorage.removeItem(key); } catch (e) { /* 忽略 */ }
        }

        function updateHint(iso) {
            if (!timeHint) return;
            timeHint.textContent = iso ? '上次儲存：' + formatTime(iso) : '';
        }

        /* ── 開啟頁面時自動代入，不詢問 ──
           使用者離開又回來，本來就期待看到自己的進度，
           多問一次是多餘的。要不要保留，交給送出前的預覽去把關。 */
        function restore() {
            var draft = load();
            if (!draft) return;
            fields.forEach(function (name) {
                writeField(form, name, draft[name]);
            });
            updateHint(draft.savedAt);
        }

        /* ── 手動儲存 ──
           回傳 true 表示真的存了，false 表示使用者取消覆蓋。 */
        function save(skipOverwriteConfirm) {
            if (confirmOverwrite && !skipOverwriteConfirm) {
                var existing = load();
                if (existing && existing.savedAt) {
                    var ok = window.confirm(
                        '已經有一份 ' + formatTime(existing.savedAt) + ' 儲存的草稿。\n' +
                        '要用現在的內容覆蓋它嗎？');
                    if (!ok) return false;
                }
            }
            var data = write();
            if (!data) return false;
            updateHint(data.savedAt);
            dirty = false;
            return true;
        }

        /* ── 髒資料追蹤 ──
           input 管文字輸入，change 管 radio 與 checkbox。 */
        form.addEventListener('input',  function () { dirty = true; });
        form.addEventListener('change', function () { dirty = true; });

        /* ── 儲存草稿按鈕 ── */
        if (saveBtn) {
            saveBtn.addEventListener('click', function () {
                if (save()) {
                    saveBtn.blur();   // 避免按鈕停在 focus 樣式，看起來像沒反應
                }
            });
        }

          /* ── 表單送出 ──
             ⚠️ 驗證失敗時 submit 事件仍可能被觸發，這時不能清草稿，
                否則使用者只是評論太長被擋下來，草稿卻不見了。
             ⚠️ 同理，表單若有 onsubmit="return confirm(...)"，使用者
                按「取消」時 submit 事件一樣會發生，只是被攔下來。
                defaultPrevented 就是用來分辨「真的要送出」與
                「已經被別人攔掉了」。 */
          form.addEventListener('submit', function (e) {
                if (e.defaultPrevented) return;      // 已被其他處理器取消

            var valid = true;
            if (window.jQuery && window.jQuery.fn && window.jQuery.fn.validate) {
                valid = window.jQuery(form).valid();
            } else if (typeof form.checkValidity === 'function') {
                valid = form.checkValidity();
            }
            if (valid) {
                clear();
                dirty = false;
            }
        });

        /* ── 攔截頁面內的連結 ──
           這是唯一能給三個選項的情境。 */
        var pendingHref = null;

        document.addEventListener('click', function (e) {
            if (!dirty) return;
            if (!e.target || !e.target.closest) return;

            var a = e.target.closest('a[href]');
            if (!a) return;

            var href = a.getAttribute('href');
            if (!href) return;
            if (href.charAt(0) === '#') return;                 // 頁內錨點
            if (href.indexOf('javascript:') === 0) return;      // 純腳本
            if (a.target === '_blank') return;                  // 開新分頁，本頁不會消失
            if (a.hasAttribute('download')) return;
            if (a.hasAttribute('data-draft-ignore')) return;    // 想放行的連結自己標

            e.preventDefault();
            pendingHref = a.href;
            ensureModal();
            modalInstance.show();
        });

        /* ── Modal 三個按鈕 ── */
        document.addEventListener('click', function (e) {
            if (!e.target || !e.target.closest) return;
            var btn = e.target.closest('[data-draft-choice]');
            if (!btn) return;

            var choice = btn.getAttribute('data-draft-choice');

            if (choice === 'cancel') {
                pendingHref = null;
                modalInstance.hide();
                return;
            }

            if (choice === 'save') {
                // 這條路徑不再問一次覆蓋，使用者已經在 Modal 上表態過了
                if (!save(true)) return;
            }

            // discard 與 save 都要離開
            dirty = false;
            modalInstance.hide();
            if (pendingHref) {
                var url = pendingHref;
                pendingHref = null;
                window.location.href = url;
            }
        });

        /* ── 關閉分頁／重新整理／瀏覽器上一頁 ──
           只能觸發瀏覽器內建的兩選項對話框，文字不可自訂。 */
        window.addEventListener('beforeunload', function (e) {
            if (!dirty) return;
            if (saveOnUnload) {
                write();          // 同步寫入，來得及
                return;           // 已存好就不必攔了
            }
            e.preventDefault();
            e.returnValue = '';   // 這行是觸發對話框的必要條件，字串內容不會被顯示
        });

        /* ── 初始化：先代入草稿，代入不算修改 ── */
        restore();
        dirty = false;

        /* ── 對外的操作 ── */
        return {
            save:  save,
            clear: function () { clear(); updateHint(null); },
            load:  load,
            isDirty: function () { return dirty; },
            setClean: function () { dirty = false; }
        };
    }

    return { init: init };

})();
