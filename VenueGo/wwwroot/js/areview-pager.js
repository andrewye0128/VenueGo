/* ============================================================
   areview-pager.js — 館方評論清單的換頁 Bar（昱，10/23）
   放置路徑：wwwroot/js/areview-pager.js（要在 areview-queue.js 之前載入）
   相依：無（樣式由這個檔案自己放進頁面，不必改共用的 site.css）

   ── 畫面在 _QueueList.cshtml 最下面的 #queuePager ──
   清單會被整塊換掉（重新載入 Partial），所以事件一律掛在 document 上（事件委派），
   目前的頁碼、總頁數每次都從 #queuePager 的 data-* 現讀。

   ── 換頁 ──
   - 按鈕點一下、或按 Page Up／Page Down：換一頁。只有回覆框（textarea）裡不攔截（那裡的 Page Up／Down 是在多行文字裡移動游標），
     有對話框開著時也不攔截。
     10/24：搜尋框、開關、下拉選單裡也改成換頁。原本也不攔截，結果在搜尋框、開關上按會捲動整頁，
     在下拉選單上按會直接選到第一個／最後一個選項（等於換了篩選條件），都不是員工要的。
     下拉選單要換選項用 ↑ ↓，搜尋框要把游標移到最前面／最後面用 Home／End。
   - 長按（按鈕或按鍵）：按下先換一頁，按住 0.3 秒出現快轉遮罩，之後每 0.5 秒換一頁（跟顧客端的評論專區同一套時間）。
     長按期間只有頁碼在動，放開才真的載入；遮罩上顯示「正在經過的那一頁」第一則的排序時間。
   - 真正載入由 areview-queue.js 負責：這裡只發出 queue:go-page 事件（detail.page），
     載入完（成功或失敗）areview-queue.js 會發出 queue:rendered，這裡再把頁碼的預覽清掉。

   ── 10/24：顧客端驗收時發現的問題，這裡一起避免 ──
   - 換頁不閃：點一下換頁，清單淡出到一半（0.15 秒），新的一頁一出現就是同樣的淡，再淡入（0.3 秒）。
     長按快轉放開後，遮罩繼續蓋著，畫一個綠色的圓圈打勾（約 0.55 秒）；勾畫完、新的一頁也畫好了，遮罩才淡出。
   - 遮罩改由這個檔案建立、放在 <body> 最後面，再對齊到清單（#queueListArea）上：
     清單載入時整塊被換掉，遮罩如果放在清單裡，會跟著被換掉，打勾動畫就會從頭重畫。
   - Bar 變矮（約 57 → 49px，上下留白 4 → 2px，按鈕維持 44px）；收合鈕跟換頁按鈕一樣寬（7rem），圖示置中。
   - 10/24 傍晚：Bar 改成固定在畫面最下面、左右滿版（左邊貼齊側欄、右邊貼齊畫面，跟上方的 header 一樣寬）。
     原本是 sticky：清單短的時候 Bar 跟著清單停在畫面中間；捲到最下面時，下面還有內容區的留白，Bar 也貼不到底。
     #queuePager 本身留在原位當「佔位」（高度跟 Bar 一樣），最後一則才不會被 Bar 蓋住。
     左邊的位置直接用版型的 CSS 變數（--sidebar-w、--sidebar-w-collapsed，在 admin.css），側欄收合時跟著一起移動，不用 JS 量。
   - 用滑鼠按收合／展開時，不把焦點移到另一顆按鈕上：不然接著按 Page Down，瀏覽器會在那顆按鈕上畫出藍框，
     看起來像它亮了。用鍵盤（Tab＋Enter）按的才移焦點，只用鍵盤的人才知道自己在哪。

   ── 收合狀態 ──
   存在 sessionStorage：同一個分頁記到分頁關掉為止，新開的分頁各自獨立（複製分頁時會連狀態一起複製，這是瀏覽器的規則）。
   狀態掛在 <html> 的 data-queue-pager-collapsed 上：清單被換掉時新的 Bar 也會照著收合，不必重新套用。
   ============================================================ */

(function () {
    'use strict';

    const HOLD_DELAY = 300;  // 按住多久算長按（出現遮罩、還不前進）
    const INTERVAL = 500;    // 長按之後每隔多久換一頁
    const STORE_KEY = 'venuego.queuePager.collapsed';
    const COLLAPSED_ATTR = 'data-queue-pager-collapsed';

    // 這些地方的 Page Up／Page Down 有自己的用途，不攔截
      // 10/24：只留回覆框（多行文字）、可編輯區、對話框、自訂的選單（role="listbox"）。搜尋框、開關、下拉選單改成會換頁（檔頭）
      const KEEP_KEYS = 'textarea, [contenteditable="true"], .modal, [role="dialog"], [role="listbox"]';
    const KEY_DIR = { PageUp: 'prev', PageDown: 'next' };

    /* ── 樣式 ───────────────────────────────────────────── */
    const css = `
/* 10/24 傍晚：#queuePager 留在原位當佔位（跟 Bar 一樣高）；Bar、展開鈕固定在畫面最下面（檔頭）。
   左邊貼齊側欄：用 admin.css 的 --sidebar-w／--sidebar-w-collapsed，手機版（992px 以下）側欄是滑出的，貼齊畫面左邊。
   z-index 1020：比 header（1030）、側欄（1040）、手機版側欄的遮罩（1035）、對話框都低。 */
.queue-pager { height: 49px; margin-top: .5rem; }
.queue-pager-bar {
  position: fixed; bottom: 0; left: var(--sidebar-w, 0px); right: 0; z-index: 1020;
  display: grid; grid-template-columns: 7rem 1fr 7rem; align-items: center;
  padding: 2px 16px; background: var(--surface, #fff);
  border-top: 1px solid var(--border, #e5e7eb);
  box-shadow: 0 -2px 8px rgba(0, 0, 0, .06);
  transition: left .2s ease;
}
.queue-pager-center { display: flex; align-items: center; justify-content: center; gap: .5rem; }
.queue-pager-btn {
  display: inline-flex; align-items: center; justify-content: center; height: 2.75rem; width: 2.75rem;
  border: 0; border-radius: .5rem; background: transparent; color: var(--bs-body-color, #212529);
  font-size: 1.25rem; cursor: pointer; user-select: none; -webkit-touch-callout: none; touch-action: manipulation;
  transition: background-color .15s, transform .15s;
}
.queue-pager-step, .queue-pager-bar > [data-pager-collapse] { width: 7rem; }
.queue-pager-btn:hover:not([aria-disabled="true"]) { background: rgba(var(--bs-primary-rgb, 13, 110, 253), .08); }
.queue-pager-btn[aria-disabled="true"] { opacity: .3; cursor: default; }
.queue-pager-btn[data-pressed="true"]:not([aria-disabled="true"]) {
  background: rgba(var(--bs-primary-rgb, 13, 110, 253), .16); color: var(--bs-primary, #0d6efd); transform: scale(1.06);
}
.queue-pager-btn:focus-visible, .queue-pager-expand:focus-visible { outline: 2px solid var(--bs-primary, #0d6efd); outline-offset: 2px; }
.queue-pager-num { min-width: 4.5rem; text-align: center; font-weight: 600; font-variant-numeric: tabular-nums; }
/* 展開鈕：跟收合鈕的圖示對齊（收合鈕從 Bar 左邊 16px 開始、寬 7rem，中心在 16px + 3.5rem；展開鈕寬 3rem） */
.queue-pager-expand {
  position: fixed; bottom: 0; left: calc(var(--sidebar-w, 0px) + 16px + 2rem); z-index: 1020;
  display: none; width: 3rem; height: 2.25rem; border: 1px solid var(--border, #e5e7eb);
  border-bottom: 0; border-radius: .5rem .5rem 0 0; background: var(--surface, #fff); font-size: 1.25rem;
  box-shadow: 0 -2px 8px rgba(0, 0, 0, .06); cursor: pointer;
  transition: left .2s ease;
}
.admin-layout.is-collapsed .queue-pager-bar { left: var(--sidebar-w-collapsed, 0px); }
.admin-layout.is-collapsed .queue-pager-expand { left: calc(var(--sidebar-w-collapsed, 0px) + 16px + 2rem); }
@media (max-width: 991.98px) {
  .queue-pager-bar, .admin-layout.is-collapsed .queue-pager-bar { left: 0; }
  .queue-pager-expand, .admin-layout.is-collapsed .queue-pager-expand { left: calc(16px + 2rem); }
}
html[${COLLAPSED_ATTR}] .queue-pager-bar { display: none; }
html[${COLLAPSED_ATTR}] .queue-pager-expand { display: inline-flex; align-items: center; justify-content: center; }
.queue-pager { pointer-events: none; }
.queue-pager-bar, .queue-pager-expand { pointer-events: auto; }

/* 長按快轉的遮罩：放在 <body> 最後面，由 JS 對齊到清單上（top、left、width、height）；文字跟著捲動停在畫面中間。
   z-index 比換頁 Bar（1020）低、比清單高，Bar 才不會被蓋住 */
.queue-ff {
  position: absolute; z-index: 1015; border-radius: .5rem; background: rgba(255, 255, 255, .75); backdrop-filter: blur(1px);
  transition: opacity .3s;
}
.queue-ff.is-leaving { opacity: 0; }
.queue-ff-text { position: sticky; top: calc(50vh - 3rem); padding: 2rem 0; text-align: center; }
.queue-ff-page { font-size: 1.75rem; font-weight: 700; font-variant-numeric: tabular-nums; }
.queue-ff-mark { margin-top: .25rem; font-size: 1.1rem; color: var(--bs-secondary-color, #6c757d); }
.queue-ff-mark:empty { display: none; }

/* 放開後的圓圈打勾：線的長度當成 1（pathLength），先用虛線藏起來，再動畫到 0，看起來像一筆畫出來 */
.queue-ff-check { display: block; width: 3rem; height: 3rem; margin: .75rem auto 0; color: var(--bs-success, #198754); }
.queue-ff-check[hidden] { display: none; }
.queue-ff-check circle, .queue-ff-check path { stroke-dasharray: 1; stroke-dashoffset: 1; }
.queue-ff-check.is-drawing circle { animation: queue-draw .35s ease-out forwards; }
.queue-ff-check.is-drawing path { animation: queue-draw .25s ease-out .3s forwards; }
@keyframes queue-draw { to { stroke-dashoffset: 0; } }

/* 點一下換頁：清單淡出、新的一頁淡入（JS 切換 class） */
#queueListArea { transition: opacity .3s; }
#queueListArea.queue-dim { opacity: .5; transition-duration: .15s; }

/* Bar 展開時，時光機的按鈕和提示往上移，不要壓在 Bar 上 */
/* 10/24 傍晚：Bar 高 49px，⏰ 停在 Bar 上方 8px（原本 76px 離太遠）；提示跟著往下，維持在 ⏰ 上方 */
html:not([${COLLAPSED_ATTR}]):has(#queuePager) .vg-tm-fab { bottom: 57px; }
html:not([${COLLAPSED_ATTR}]):has(#queuePager) .vg-tm-toast { bottom: 109px; }

@media (prefers-reduced-motion: reduce) {
  .queue-pager-btn, .queue-pager-bar, .queue-pager-expand { transition: none; }
  .queue-ff-check.is-drawing circle, .queue-ff-check.is-drawing path { animation: none; stroke-dashoffset: 0; }
}
`;
    const style = document.createElement('style');
    style.textContent = css;
    document.head.appendChild(style);

    /* ── 10/24：快轉遮罩（放在 <body> 最後面，見檔頭）──────── */
    const overlay = document.createElement('div');
    overlay.className = 'queue-ff';
    overlay.hidden = true;
    overlay.setAttribute('aria-hidden', 'true');
    overlay.innerHTML =
        '<div class="queue-ff-text">' +
        '<div class="queue-ff-page"></div>' +
        '<div class="queue-ff-mark"></div>' +
        '<svg class="queue-ff-check" viewBox="0 0 52 52" fill="none" stroke="currentColor" stroke-width="3.5"' +
        ' stroke-linecap="round" stroke-linejoin="round" hidden>' +
        '<circle cx="26" cy="26" r="23" pathLength="1"></circle><path d="M15 27 L23 35 L38 18" pathLength="1"></path></svg>' +
        '</div>';
    document.body.appendChild(overlay);
    const overlayPage = overlay.querySelector('.queue-ff-page');
    const overlayMark = overlay.querySelector('.queue-ff-mark');
    const overlayCheck = overlay.querySelector('.queue-ff-check');

    // 對齊到清單上。清單換掉、捲回清單開頭之後都要重新對齊
    function placeOverlay() {
        const area = document.getElementById('queueListArea');
        if (!area) return;
        const r = area.getBoundingClientRect();
        overlay.style.top = (r.top + window.scrollY) + 'px';
        overlay.style.left = (r.left + window.scrollX) + 'px';
        overlay.style.width = r.width + 'px';
        overlay.style.height = r.height + 'px';
    }
    window.addEventListener('resize', function () {
        if (!overlay.hidden) placeOverlay();
    });

    /* 放開之後：遮罩繼續蓋著、畫打勾，「勾畫完」和「新的一頁畫好」都做到了才淡出（檔頭） */
    const CHECK_MS = 550;       // 圓圈 0.35 秒，勾從 0.3 秒開始畫 0.25 秒
    const LEAVE_MS = 300;       // 淡出的時間，跟 CSS 的 .queue-ff transition 一樣
    let landing = false;        // 放開了、等新的一頁
    let leaving = false;        // 正在淡出
    let checkDone = false;
    let pageReady = false;
    let checkTimer = null;
    let landingTimer = null;
    let leaveTimer = null;

    function startLanding() {
        landing = true;
        leaving = false;
        checkDone = false;
        pageReady = false;
        clearTimeout(leaveTimer);
        overlay.classList.remove('is-leaving');
        overlayCheck.removeAttribute('hidden');   // SVG 沒有 .hidden 屬性，要直接改 attribute
        overlayCheck.classList.remove('is-drawing');
        void overlayCheck.getBoundingClientRect();   // 讓瀏覽器先畫一次「沒有動畫」的狀態，動畫才會從頭開始
        overlayCheck.classList.add('is-drawing');
        clearTimeout(checkTimer);
        checkTimer = setTimeout(function () {
            checkDone = true;
            if (pageReady) endLanding();
        }, CHECK_MS);
        clearTimeout(landingTimer);
        // 保險：一直沒有載入完的話，10 秒後還是拿掉，不要永遠蓋著
        landingTimer = setTimeout(endLanding, 10000);
    }

    function endLanding() {
        if (!landing) return;
        landing = false;
        leaving = true;
        clearTimeout(checkTimer);
        clearTimeout(landingTimer);
        overlay.classList.add('is-leaving');
        leaveTimer = setTimeout(function () {
            leaving = false;
            overlay.classList.remove('is-leaving');
            render();
        }, LEAVE_MS);
    }

    /* 點一下換頁的淡出、淡入：換頁前把清單調淡；載入完，新的清單先「直接」設成一樣淡（不要動畫），再淡入 */
    let fadePending = false;
    function dimList() {
        const area = document.getElementById('queueListArea');
        if (!area) return;
        area.classList.add('queue-dim');
        fadePending = true;
    }
    function undimList() {
        if (!fadePending) return;
        fadePending = false;
        const area = document.getElementById('queueListArea');
        if (!area) return;
        area.style.transition = 'none';
        area.classList.add('queue-dim');
        void area.offsetWidth;          // 讓「淡」先生效
        area.style.transition = '';
        area.classList.remove('queue-dim');
    }

    /* ── 收合 ───────────────────────────────────────────── */
    function readCollapsed() {
        try {
            return sessionStorage.getItem(STORE_KEY) === '1';
        } catch (e) {
            return false;   // 瀏覽器封鎖 sessionStorage：當成沒有紀錄
        }
    }
    function applyCollapsed(value) {
        document.documentElement.toggleAttribute(COLLAPSED_ATTR, value);
    }
    function setCollapsed(value) {
        applyCollapsed(value);
        try {
            if (value) sessionStorage.setItem(STORE_KEY, '1');
            else if (!value) sessionStorage.removeItem(STORE_KEY);
        } catch (e) {
            // 存不了就只在這一頁有效
        }
    }
    applyCollapsed(readCollapsed());

    /* ── 目前的頁碼（每次從畫面現讀）────────────────────── */
    function pagerEl() {
        return document.getElementById('queuePager');
    }
    function readState() {
        const el = pagerEl();
        if (!el) return null;
        let marks = [];
        try {
            marks = JSON.parse(el.dataset.marks || '[]');
        } catch (e) {
            marks = [];   // 讀不到快轉資訊時，遮罩只顯示頁碼
        }
        return { page: Number(el.dataset.page), total: Number(el.dataset.totalPages), marks: marks };
    }

    /* ── 長按快轉 ───────────────────────────────────────── */
    let activeDir = null;   // 正在按的方向（'prev'／'next'），沒有在按是 null
    let repeating = false;  // 已經認定是長按
    let timer = null;
    let previewPage = null; // 長按期間「頁碼走到哪」，還沒載入；null＝沒有在快轉

    function shownPage(st) {
        return previewPage !== null ? previewPage : st.page;
    }

    function step(dir) {
        const st = readState();
        if (!st) return;
        const next = shownPage(st) + (dir === 'next' ? 1 : -1);
        if (next < 1 || next > st.total) return;   // 到頭了：停在原地
        previewPage = next;
        render();
    }

    function hold() {
        repeating = true;
        render();
        timer = setTimeout(tick, INTERVAL);
    }

    function tick() {
        step(activeDir);
        timer = setTimeout(tick, INTERVAL);
    }

    function start(dir) {
        if (activeDir !== null) stop();   // 換方向：先把上一個結束
        if (!readState()) return;
        activeDir = dir;
        repeating = false;
        step(dir);
        render();
        timer = setTimeout(hold, HOLD_DELAY);
    }

    function stop() {
        if (activeDir === null) return;
        clearTimeout(timer);
        timer = null;
        const overlayWasShown = repeating && previewPage !== null;   // 10/24：先記下來，下一行就改掉了
        activeDir = null;
        repeating = false;
        const st = readState();
        if (st && previewPage !== null && previewPage !== st.page) {
            // 10/24：遮罩出現過的話，繼續蓋著並畫打勾；清單一律先調淡（檔頭）
            if (overlayWasShown) startLanding();
            dimList();
            // 頁碼停在 previewPage，等 areview-queue.js 載入完（queue:rendered）再清掉，畫面才不會閃回舊頁碼
            document.dispatchEvent(new CustomEvent('queue:go-page', { detail: { page: previewPage } }));
        } else {
            previewPage = null;
        }
        render();
    }

    // 日期：10月19日 14:30（不是今年才加上年份）；置頂的加註
    function markText(mark) {
        if (!mark || typeof mark.time !== 'string') return '';
        const m = mark.time.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/);
        if (!m) return '';
        const year = Number(m[1]);
        const date = (year === new Date().getFullYear() ? '' : year + '年') + Number(m[2]) + '月' + Number(m[3]) + '日 ' + m[4] + ':' + m[5];
        return mark.pinned ? '置頂・' + date : date;
    }

    function render() {
        const st = readState();
        const el = pagerEl();
        if (!st || !el) return;
        const shown = shownPage(st);

        const current = el.querySelector('[data-pager-current]');
        if (current) current.textContent = String(shown);

        el.querySelectorAll('[data-pager-dir]').forEach(function (btn) {
            const dir = btn.dataset.pagerDir;
            const atEdge = (dir === 'prev' && shown <= 1) || (dir === 'next' && shown >= st.total);
            // 用 aria-disabled 不用 disabled：按住的途中按鈕若被停用，瀏覽器就不會再送「放開」給它，快轉會停不下來
            if (atEdge) btn.setAttribute('aria-disabled', 'true');
            else btn.removeAttribute('aria-disabled');
            if (activeDir === dir) btn.setAttribute('data-pressed', 'true');
            else btn.removeAttribute('data-pressed');
        });

        // 10/24：遮罩改成 <body> 最後面的那一個（檔頭）。淡出的途中不更新文字，維持放開時的樣子
        const holding = repeating && previewPage !== null;
        if (holding || landing) {
            if (holding) {
                // 又開始按住：上一次的打勾、淡出都不要了
                clearTimeout(leaveTimer);
                leaving = false;
                overlay.classList.remove('is-leaving');
                overlayCheck.toggleAttribute('hidden', !landing);
            }
            placeOverlay();
            overlay.hidden = false;
            overlayPage.textContent = '第 ' + shown + ' / ' + st.total + ' 頁';
            const text = markText(st.marks[shown - 1]);
            overlayMark.textContent = text ? '從「' + text + '」開始' : '';
        } else if (!leaving) {
            overlay.hidden = true;
            overlayCheck.setAttribute('hidden', '');
        }
    }

    // 新的一頁載入完（或載入失敗）：預覽的頁碼不再需要
    document.addEventListener('queue:rendered', function () {
        if (activeDir === null) previewPage = null;
        render();
        undimList();   // 10/24：新的一頁淡入
        // 10/24：遮罩對齊到新的清單，等一個畫格確定新的一頁畫出來了，才算「新的一頁畫好」
        if (landing) {
            requestAnimationFrame(function () {
                pageReady = true;
                if (checkDone) endLanding();
            });
        }
    });

    /* ── 滑鼠、手指 ─────────────────────────────────────── */
    let fromPointer = false;   // 接下來的 click 是滑鼠、手指放開時補送的（不是鍵盤）

    document.addEventListener('pointerdown', function (e) {
        const btn = e.target.closest('[data-pager-dir]');
        if (!btn) return;
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        e.preventDefault();   // 不要選取文字
        // pointer capture：按下之後游標移出按鈕，放開的事件還是會送回這顆按鈕
        if (btn.setPointerCapture) btn.setPointerCapture(e.pointerId);
        fromPointer = true;
        start(btn.dataset.pagerDir);
    });
    ['pointerup', 'pointercancel', 'lostpointercapture'].forEach(function (type) {
        document.addEventListener(type, function () { stop(); });
    });
    document.addEventListener('contextmenu', function (e) {
        if (e.target.closest('[data-pager-dir]')) e.preventDefault();   // 手機長按會跳出系統選單
    });

    // 10/24：收合／展開是不是用滑鼠、手指按的（是的話不移焦點，見檔頭）
    let toggleByPointer = false;
    document.addEventListener('pointerdown', function (e) {
        toggleByPointer = !!e.target.closest('[data-pager-collapse], [data-pager-expand]');
    });

    document.addEventListener('click', function (e) {
        if (e.target.closest('[data-pager-collapse]')) {
            setCollapsed(true);
            const expand = document.querySelector('[data-pager-expand]');
            if (expand && !toggleByPointer) expand.focus();
            toggleByPointer = false;
            return;
        }
        if (e.target.closest('[data-pager-expand]')) {
            setCollapsed(false);
            const collapse = document.querySelector('[data-pager-collapse]');
            if (collapse && !toggleByPointer) collapse.focus();
            toggleByPointer = false;
            return;
        }
        const btn = e.target.closest('[data-pager-dir]');
        if (!btn) return;
        if (fromPointer) {
            fromPointer = false;   // 滑鼠、手指已經在 pointerdown 處理過了
            return;
        }
        // 鍵盤（Tab 到按鈕按 Enter／空白鍵）：當成點一下
        start(btn.dataset.pagerDir);
        stop();
    });

    /* ── 鍵盤 Page Up／Page Down ─────────────────────────── */
    document.addEventListener('keydown', function (e) {
        if ((e.key === 'Enter' || e.key === ' ') && e.target.closest && e.target.closest('[data-pager-dir]')) {
            fromPointer = false;   // 接下來的 click 是鍵盤送的
            return;
        }
        const dir = KEY_DIR[e.key];
        if (!dir || !pagerEl()) return;
        if (e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) return;
        if (e.target instanceof Element && e.target.closest(KEEP_KEYS)) return;
        if (document.querySelector('.modal.show')) return;
        e.preventDefault();   // 不要讓整頁上下捲動
        if (e.repeat) return; // 按住時瀏覽器送來的重複事件：由上面的計時器控制節奏
        start(dir);
    });
    document.addEventListener('keyup', function (e) {
        if (KEY_DIR[e.key] === activeDir) stop();
    });

    // 切到別的視窗、分頁時可能收不到「放開」，一律當成放開
    window.addEventListener('blur', stop);
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'hidden') stop();
    });
})();
