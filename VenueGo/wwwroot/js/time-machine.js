/*
 * 開發用時光機：提示條、「回到現在」確認視窗、跨分頁同步（昱）
 *
 * 由 Views/Shared/_TimeMachineBanner_Claude.cshtml（後台）、_TimeMachineFloat_Claude.cshtml（登入頁）載入。
 * 前台 Vue 不用這支，改用 src/composables/useSiteClock.js（整合在 TopBar 的時間顯示裡）。
 * <script> 上的 data-* 決定行為：
 *   data-mode   inline＝後台黃色提示條（放在 #vgTimeMachine）
 *               float ＝畫面最上方的細長條（登入頁這種沒有用 _Layout 的頁面）
 *               panel ＝時光機面板自己用，不畫提示條
 *   data-api    API 位址，預設 /api/dev/time
 *   data-state  伺服器先算好的狀態（JSON），省一次請求，畫面也不會閃
 *
 * 怎麼做到「別的分頁一調整，這裡馬上更新」：
 *   1. BroadcastChannel：同一個網站（同網址開頭）的分頁、小視窗互相廣播，幾乎即時
 *   2. postMessage：小視窗通知「打開它的那一頁」，前台 Vue（localhost:5173）也收得到
 *   3. 切回分頁、視窗取得焦點、從上一頁回來時，各問一次伺服器
 *   4. 保險：每 15 秒問一次（分頁在背景時不問）
 *
 * 樣式用 JS 插進 <head>，class 都是 vg-tm- 開頭，不會和 Bootstrap、Tailwind 互相影響。
 * 對外提供 window.VenueGoTimeMachine，給面板（time-machine-panel.js）用。
 */
(function () {
    'use strict';

    if (window.VenueGoTimeMachine) return;   // 同一頁載入兩次時只跑一次

    const script = document.currentScript;
    const options = {
        mode: (script && script.dataset.mode) || 'inline',
        api: ((script && script.dataset.api) || '/api/dev/time').replace(/\/$/, ''),
    };

    const CHANNEL_NAME = 'venuego-time-machine';
    const MESSAGE_TYPE = 'venuego:time-machine-changed';
    const POPUP_NAME = 'venuego-time-machine';
    const POLL_MS = 15000;
    const WEEKDAYS = '日一二三四五六';

    let state = null;          // 伺服器最後一次回傳的狀態
    let receivedAt = 0;        // 收到 state 的那一刻（performance.now()），用來讓時鐘自己往前走
    let pageStamp = null;      // 這一頁打開時的 stamp；不一樣代表頁面內容是用舊時間算的
    let stopped = false;       // API 回 404（不是開發環境）就整個停掉
    let lastFetchAt = 0;
    const listeners = [];

    // ═══════════════════════════════════════════════════
    //  時間的小工具
    //  伺服器給的是台北時間字串（不帶時區）。這裡一律當成 UTC 來算、用 getUTC* 讀，
    //  就不會被瀏覽器所在的時區影響。
    // ═══════════════════════════════════════════════════

    function parseTime(text) {
        const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})/.exec(text || '');
        return m ? Date.UTC(+m[1], +m[2] - 1, +m[3], +m[4], +m[5], +m[6]) : NaN;
    }

    /** 伺服器時間字串＋收到之後經過的時間＝此刻的時間（毫秒）。 */
    function liveMs(text) {
        return parseTime(text) + (performance.now() - receivedAt);
    }

    function timeParts(ms) {
        const d = new Date(ms);
        return {
            year: d.getUTCFullYear(), month: d.getUTCMonth() + 1, day: d.getUTCDate(),
            hour: d.getUTCHours(), minute: d.getUTCMinutes(), second: d.getUTCSeconds(),
            weekday: d.getUTCDay(),
        };
    }

    const pad = (n) => String(n).padStart(2, '0');

    /** 2026/10/30（五）16:45:12 */
    function formatFull(ms) {
        const p = timeParts(ms);
        return `${p.year}/${pad(p.month)}/${pad(p.day)}（${WEEKDAYS[p.weekday]}）${pad(p.hour)}:${pad(p.minute)}:${pad(p.second)}`;
    }

    /** 10/30（五）16:45:12，浮動小框用 */
    function formatShort(ms) {
        const p = timeParts(ms);
        return `${p.month}/${p.day}（${WEEKDAYS[p.weekday]}）${pad(p.hour)}:${pad(p.minute)}:${pad(p.second)}`;
    }

    // ═══════════════════════════════════════════════════
    //  和伺服器溝通
    // ═══════════════════════════════════════════════════

    async function request(method, path, body) {
        const init = {
            method,
            cache: 'no-store',
            credentials: 'same-origin',
            headers: { Accept: 'application/json' },
        };
        if (body !== undefined) {
            init.headers['Content-Type'] = 'application/json';
            init.body = JSON.stringify(body);
        }

        const response = await fetch(options.api + path, init);
        let json = null;
        try { json = await response.json(); } catch { json = null; }

        if (response.status === 404) stop();
        if (!response.ok || !json || !json.success) {
            const error = new Error((json && json.message) || `時光機沒有回應（HTTP ${response.status}）`);
            error.status = response.status;
            throw error;
        }
        return json;   // { success, message, data }
    }

    /** 問伺服器目前狀態。網站重新啟動中、斷線時保留舊狀態，不要讓提示條閃掉。 */
    async function refresh(source) {
        if (stopped) return state;
        lastFetchAt = performance.now();
        try {
            const json = await request('GET', '');
            setState(json.data, source || 'remote');
        } catch {
            // try/catch 的決定：輪詢失敗很正常（網站正在重新啟動），安靜地等下一次就好
        }
        return state;
    }

    /** 自己這一頁做了調整：更新畫面，並通知其他分頁和打開這個小視窗的頁面。 */
    function applyLocalChange(json) {
        setState(json.data, 'local');
        broadcast(json.data);
        return json;
    }

    const api = {
        travel: (parts) => request('POST', '/travel', parts).then(applyLocalChange),
        shift: (minutes) => request('POST', '/shift', { minutes }).then(applyLocalChange),
        reset: () => request('POST', '/reset', {}).then(applyLocalChange),
    };

    // ═══════════════════════════════════════════════════
    //  跨分頁同步
    // ═══════════════════════════════════════════════════

    let channel = null;
    try {
        channel = new BroadcastChannel(CHANNEL_NAME);
        channel.onmessage = (event) => {
            if (event.data && event.data.type === MESSAGE_TYPE && event.data.state) {
                setState(event.data.state, 'remote');
            }
        };
    } catch {
        channel = null;   // 很舊的瀏覽器沒有 BroadcastChannel，靠輪詢就好
    }

    function broadcast(next) {
        if (channel) channel.postMessage({ type: MESSAGE_TYPE, state: next });

        // 小視窗和打開它的頁面互相通知。對方可能是前台（不同網址開頭，BroadcastChannel 傳不過去），
        // 所以只送「有變」，讓對方自己去問伺服器
        try {
            if (window.opener && !window.opener.closed) window.opener.postMessage({ type: MESSAGE_TYPE }, '*');
            if (popup && !popup.closed) popup.postMessage({ type: MESSAGE_TYPE }, '*');
        } catch {
            // opener 已經換頁到別的網站時可能會丟例外，不影響這邊
        }
    }

    // 別人送來的「有變」：不相信內容，自己去問伺服器（任何網頁都能 postMessage 給我們）
    window.addEventListener('message', (event) => {
        if (event.data && event.data.type === MESSAGE_TYPE) refresh('remote');
    });

    document.addEventListener('visibilitychange', () => {
        if (!document.hidden) refresh('remote');
    });

    window.addEventListener('focus', () => {
        if (performance.now() - lastFetchAt > 2000) refresh('remote');
    });

    // 按「上一頁」回來時，瀏覽器可能直接拿快取的整頁出來（bfcache），要重新問
    window.addEventListener('pageshow', (event) => {
        if (event.persisted) refresh('remote');
    });

    const pollTimer = setInterval(() => {
        if (!document.hidden) refresh('remote');
    }, POLL_MS);

    function stop() {
        stopped = true;
        clearInterval(pollTimer);
        clearInterval(tickTimer);
        if (channel) channel.close();
        if (ui.root) ui.root.remove();
        if (ui.fab) ui.fab.remove();
    }

    // ═══════════════════════════════════════════════════
    //  狀態
    // ═══════════════════════════════════════════════════

    function setState(next, source) {
        if (!next) return;
        if (!next.available) { stop(); return; }

        const previous = state;
        state = next;
        receivedAt = performance.now();
        if (pageStamp === null) pageStamp = next.stamp;

        render();

        if (previous && previous.stamp !== next.stamp) {
            if (source === 'remote' && previous.traveling && !next.traveling) {
                toast('時光機已在其他視窗回到現在。這頁的內容還是舊時間算的。', true);
            }
            listeners.forEach((fn) => {
                try { fn(next, previous, source); } catch (e) { console.error(e); }
            });
        }
    }

    // ═══════════════════════════════════════════════════
    //  畫面
    // ═══════════════════════════════════════════════════

    const ui = { root: null, fab: null, site: null, offset: null, stale: null, dialog: null };

    function injectStyles() {
        if (document.getElementById('vgTimeMachineStyles')) return;
        const style = document.createElement('style');
        style.id = 'vgTimeMachineStyles';
        style.textContent = `
.vg-tm-bar, .vg-tm-fab, .vg-tm-toast, .vg-tm-dialog {
  --vg-tm-bg: #fff7d6; --vg-tm-border: #f2c94c; --vg-tm-ink: #5c4300; --vg-tm-strong: #3d2c00;
  --vg-tm-accent: #b45309; --vg-tm-accent-hover: #92400e;
  font-family: inherit; font-size: 14px; line-height: 1.5; box-sizing: border-box;
}
.vg-tm-bar *, .vg-tm-dialog *, .vg-tm-toast * { box-sizing: border-box; }
.vg-tm-bar[hidden], .vg-tm-fab[hidden], .vg-tm-bar [hidden] { display: none !important; }

/* 後台：黃色提示條，和 Header 一起黏在最上面 */
.vg-tm-bar {
  display: flex; flex-wrap: wrap; align-items: center; gap: 4px 12px;
  padding: 8px 16px; background: var(--vg-tm-bg); color: var(--vg-tm-ink);
  border-bottom: 1px solid var(--vg-tm-border);
}
.vg-tm-bar.vg-tm-inline { position: sticky; top: 0; z-index: 1031; }
html.vg-tm-has-bar .admin-header { top: var(--vg-tm-bar-h, 0px); }
.vg-tm-bar strong { color: var(--vg-tm-strong); font-weight: 700; font-variant-numeric: tabular-nums; }
.vg-tm-offset { color: var(--vg-tm-ink); opacity: .85; }
.vg-tm-stale { display: inline-flex; align-items: center; gap: 6px; padding: 1px 8px; border-radius: 999px;
  background: #fde68a; color: var(--vg-tm-strong); font-size: 13px; }
.vg-tm-actions { display: inline-flex; gap: 6px; margin-left: auto; }
.vg-tm-btn {
  appearance: none; border: 1px solid var(--vg-tm-border); background: #fff; color: var(--vg-tm-accent);
  font: inherit; font-weight: 600; padding: 2px 10px; border-radius: 6px; cursor: pointer; line-height: 1.5;
}
.vg-tm-btn:hover { background: #fffbeb; color: var(--vg-tm-accent-hover); }
.vg-tm-btn:focus-visible, .vg-tm-fab:focus-visible { outline: 2px solid var(--vg-tm-accent); outline-offset: 2px; }
.vg-tm-link { appearance: none; border: 0; background: none; padding: 0; font: inherit; font-weight: 600;
  color: var(--vg-tm-accent); text-decoration: underline; cursor: pointer; }

/* 登入頁這種沒有 _Layout 的頁面：畫面最上方的細長條，頁面內容往下推，不會被蓋住 */
.vg-tm-bar.vg-tm-float {
  position: fixed; top: 0; left: 0; right: 0; z-index: 2000;
  flex-wrap: nowrap; justify-content: safe center; gap: 10px; padding: 3px 12px;
  font-size: 13px; white-space: nowrap; overflow: hidden;
}
.vg-tm-float .vg-tm-actions { margin-left: 0; gap: 4px; }
.vg-tm-float .vg-tm-btn { padding: 0 8px; font-size: 12px; }
.vg-tm-float .vg-tm-stale { padding: 0 6px; font-size: 12px; }
html.vg-tm-has-top body { padding-top: var(--vg-tm-bar-h, 0px); }
@media (max-width: 480px) { .vg-tm-float .vg-tm-offset, .vg-tm-float .vg-tm-stale { display: none !important; } }

/* 沒旅行時右下角的 ⏰ */
.vg-tm-fab {
  position: fixed; right: 16px; bottom: 16px; z-index: 2000; width: 40px; height: 40px; padding: 0;
  display: grid; place-items: center; border-radius: 50%; border: 1px solid var(--vg-tm-border);
  background: var(--vg-tm-bg); font-size: 20px; line-height: 1; cursor: pointer; opacity: .55;
  box-shadow: 0 2px 8px rgba(0,0,0,.12); transition: opacity .15s ease;
}
.vg-tm-fab:hover, .vg-tm-fab:focus-visible { opacity: 1; }

/* 「回到現在」確認視窗 */
.vg-tm-dialog {
  width: min(400px, calc(100vw - 32px)); padding: 0; border: 0; border-radius: 14px;
  box-shadow: 0 20px 60px rgba(0,0,0,.25); color: #1f2937; background: #fff;
}
.vg-tm-dialog::backdrop { background: rgba(15,23,42,.45); }
.vg-tm-dialog form { margin: 0; padding: 20px 22px 18px; }
.vg-tm-dialog h2 { margin: 0 0 12px; font-size: 18px; font-weight: 700; color: #111827; }
.vg-tm-dialog p { margin: 0 0 6px; }
.vg-tm-dialog .vg-tm-big { font-size: 20px; font-weight: 700; color: #111827; font-variant-numeric: tabular-nums; margin-bottom: 4px; }
.vg-tm-dialog .vg-tm-muted { color: #6b7280; font-size: 13px; font-variant-numeric: tabular-nums; }
.vg-tm-dialog .vg-tm-error { color: #b42318; font-size: 13px; margin-top: 8px; }
.vg-tm-dialog-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 18px; }
.vg-tm-dialog button {
  appearance: none; font: inherit; font-weight: 600; padding: 7px 16px; border-radius: 8px; cursor: pointer; line-height: 1.4;
}
.vg-tm-dialog .vg-tm-cancel { border: 1px solid #d1d5db; background: #fff; color: #374151; }
.vg-tm-dialog .vg-tm-cancel:hover { background: #f9fafb; }
.vg-tm-dialog .vg-tm-confirm { border: 1px solid #b45309; background: #b45309; color: #fff; }
.vg-tm-dialog .vg-tm-confirm:hover { background: #92400e; border-color: #92400e; }
.vg-tm-dialog .vg-tm-confirm:disabled { opacity: .6; cursor: progress; }
.vg-tm-dialog button:focus-visible { outline: 2px solid #b45309; outline-offset: 2px; }

/* 右下角提示訊息：停留 5 秒，再花 2 秒慢慢淡出 */
.vg-tm-toast {
  position: fixed; right: 16px; bottom: 68px; z-index: 2001; max-width: min(380px, calc(100vw - 32px));
  display: flex; align-items: center; gap: 10px; padding: 10px 14px; border-radius: 10px;
  background: #1f2937; color: #f9fafb; box-shadow: 0 6px 24px rgba(0,0,0,.2);
  opacity: 1; transition: opacity 2s ease;
}
.vg-tm-toast.vg-tm-fading { opacity: 0; }
.vg-tm-toast .vg-tm-link { color: #fcd34d; }
@media (prefers-reduced-motion: reduce) { .vg-tm-toast { transition: none; } }
`;
        document.head.appendChild(style);
    }

    function buildUi() {
        if (options.mode === 'panel') return;
        injectStyles();

        let host = null;
        if (options.mode === 'inline') host = document.getElementById('vgTimeMachine');
        const inline = !!host;

        const root = document.createElement('div');
        root.className = 'vg-tm-bar ' + (inline ? 'vg-tm-inline' : 'vg-tm-float');
        root.setAttribute('role', 'region');
        root.setAttribute('aria-label', '時光機');
        root.hidden = true;
        root.innerHTML = `
<span aria-hidden="true">⏰</span>
<span>${inline ? '時光機啟動中：網站時間' : '時光機'} <strong data-tm="site"></strong>
  <span class="vg-tm-offset" data-tm="offset"></span></span>
<span class="vg-tm-stale" data-tm="stale" hidden title="這一頁是在調整時間之前打開的，上面的內容還是用舊的時間算的">${inline ? '內容是舊時間算的' : '舊時間'}
  <button type="button" class="vg-tm-link" data-tm-action="reload">重新整理</button></span>
<span class="vg-tm-actions">
  <button type="button" class="vg-tm-btn" data-tm-action="panel">調整</button>
  <button type="button" class="vg-tm-btn" data-tm-action="reset">回到現在</button>
</span>`;

        if (inline) host.replaceWith(root);
        else document.body.appendChild(root);

        const fab = document.createElement('button');
        fab.type = 'button';
        fab.className = 'vg-tm-fab';
        fab.dataset.tmAction = 'panel';
        fab.title = '時光機（開發用）';
        fab.setAttribute('aria-label', '打開時光機');
        fab.textContent = '⏰';
        fab.hidden = true;
        document.body.appendChild(fab);

        ui.root = root;
        ui.fab = fab;
        ui.site = root.querySelector('[data-tm="site"]');
        ui.offset = root.querySelector('[data-tm="offset"]');
        ui.stale = root.querySelector('[data-tm="stale"]');

        [root, fab].forEach((el) => el.addEventListener('click', onAction));

        // 提示條的高度會變（視窗變窄時換行），Header 要黏在它下面、頁面內容要往下推，所以隨時量
        if ('ResizeObserver' in window) {
            new ResizeObserver(updateBarHeight).observe(root);
        }
    }

    function updateBarHeight() {
        if (!ui.root) return;
        const height = ui.root.hidden ? 0 : ui.root.offsetHeight;
        const inline = ui.root.classList.contains('vg-tm-inline');
        document.documentElement.style.setProperty('--vg-tm-bar-h', height + 'px');
        document.documentElement.classList.toggle('vg-tm-has-bar', inline && height > 0);
        document.documentElement.classList.toggle('vg-tm-has-top', !inline && height > 0);
    }

    function onAction(event) {
        const button = event.target.closest('[data-tm-action]');
        if (!button) return;
        const action = button.dataset.tmAction;
        if (action === 'panel') openPanel();
        else if (action === 'reset') confirmReset();
        else if (action === 'reload') location.reload();
    }

    function render() {
        if (!ui.root || !state) return;

        ui.root.hidden = !state.traveling;
        ui.fab.hidden = state.traveling;

        if (state.traveling) {
            ui.offset.textContent = ui.root.classList.contains('vg-tm-float')
                ? `・${state.offsetText}`
                : `（比真實時間${state.offsetText}）`;
            ui.stale.hidden = state.stamp === pageStamp;
            tick();
        }
        updateBarHeight();
    }

    function tick() {
        if (!state) return;
        const site = liveMs(state.siteNow);
        if (ui.site && state.traveling) {
            ui.site.textContent = ui.root.classList.contains('vg-tm-float') ? formatShort(site) : formatFull(site);
        }
        if (ui.dialog && ui.dialog.open) {
            ui.dialog.querySelector('[data-tm="real"]').textContent = formatFull(liveMs(state.realNow));
            ui.dialog.querySelector('[data-tm="site2"]').textContent = formatFull(site);
        }
    }

    const tickTimer = setInterval(tick, 1000);

    // ═══════════════════════════════════════════════════
    //  時光機面板（小視窗）
    // ═══════════════════════════════════════════════════

    let popup = null;

    function openPanel() {
        const url = (state && state.panelUrl) || '/DevTools/Time';

        // 已經開著就拉到前面，不要重新載入（裡面可能填到一半）
        if (popup && !popup.closed) { popup.focus(); return; }

        const width = 460;
        const height = 720;
        const left = Math.max(0, (window.screenX || 0) + (window.outerWidth || width) - width - 24);
        const top = Math.max(0, (window.screenY || 0) + 60);
        popup = window.open(url, POPUP_NAME, `popup=yes,width=${width},height=${height},left=${left},top=${top}`);

        // 小視窗被瀏覽器擋掉時，改開新分頁
        if (!popup) window.open(url, '_blank');
        else popup.focus();
    }

    // ═══════════════════════════════════════════════════
    //  「回到現在」確認視窗
    // ═══════════════════════════════════════════════════

    function buildDialog() {
        injectStyles();
        const dialog = document.createElement('dialog');
        dialog.className = 'vg-tm-dialog';
        dialog.setAttribute('aria-labelledby', 'vgTmDialogTitle');
        dialog.innerHTML = `
<form method="dialog">
  <h2 id="vgTmDialogTitle">回到現在？</h2>
  <p>網站時間會回到真實時間：</p>
  <p class="vg-tm-big" data-tm="real"></p>
  <p class="vg-tm-muted">目前的網站時間：<span data-tm="site2"></span></p>
  <p class="vg-tm-muted">會留在這一頁；頁面上已經算好的內容，要重新整理才會更新。</p>
  <p class="vg-tm-error" data-tm="error" role="alert" hidden></p>
  <div class="vg-tm-dialog-actions">
    <button type="submit" value="cancel" class="vg-tm-cancel">取消</button>
    <button type="button" class="vg-tm-confirm" data-tm="confirm">回到現在</button>
  </div>
</form>`;
        document.body.appendChild(dialog);

        dialog.querySelector('[data-tm="confirm"]').addEventListener('click', async (event) => {
            const button = event.currentTarget;
            const error = dialog.querySelector('[data-tm="error"]');
            button.disabled = true;
            error.hidden = true;
            try {
                const json = await api.reset();
                dialog.close('ok');
                const p = timeParts(parseTime(json.data.siteNow));
                toast(`已回到現在（${p.year}/${pad(p.month)}/${pad(p.day)} ${pad(p.hour)}:${pad(p.minute)}:${pad(p.second)}）。`, true);
            } catch (e) {
                // try/catch 的決定：失敗時視窗不關，把原因寫在視窗裡，讓使用者可以再按一次
                error.textContent = e.message || '回到現在失敗，請再試一次';
                error.hidden = false;
            } finally {
                button.disabled = false;
            }
        });

        ui.dialog = dialog;
        return dialog;
    }

    async function confirmReset() {
        const dialog = ui.dialog || buildDialog();
        dialog.querySelector('[data-tm="error"]').hidden = true;
        tick();
        dialog.showModal();
        dialog.querySelector('[data-tm="confirm"]').focus();
        refresh('remote');   // 順便更新一次，確保顯示的真實時間是準的
    }

    // ═══════════════════════════════════════════════════
    //  右下角提示訊息
    // ═══════════════════════════════════════════════════

    let toastTimer = null;

    function toast(message, offerReload) {
        if (options.mode === 'panel') return;   // 面板有自己的訊息區
        injectStyles();
        document.querySelectorAll('.vg-tm-toast').forEach((el) => el.remove());
        clearTimeout(toastTimer);

        const el = document.createElement('div');
        el.className = 'vg-tm-toast';
        el.setAttribute('role', 'status');
        el.textContent = message;
        if (offerReload) {
            const reload = document.createElement('button');
            reload.type = 'button';
            reload.className = 'vg-tm-link';
            reload.textContent = '重新整理';
            reload.addEventListener('click', () => location.reload());
            el.appendChild(reload);
        }
        document.body.appendChild(el);

        // 停 5 秒，再花 2 秒淡出；滑鼠移上去就先不消失
        const startFade = () => {
            toastTimer = setTimeout(() => {
                el.classList.add('vg-tm-fading');
                setTimeout(() => el.remove(), 2000);
            }, 5000);
        };
        el.addEventListener('mouseenter', () => { clearTimeout(toastTimer); el.classList.remove('vg-tm-fading'); });
        el.addEventListener('mouseleave', startFade);
        startFade();
    }

    // ═══════════════════════════════════════════════════
    //  開始
    // ═══════════════════════════════════════════════════

    window.VenueGoTimeMachine = {
        /** 目前狀態（可能是 null）。 */
        get state() { return state; },
        /** 把伺服器時間字串換成「此刻」的毫秒數（UTC 算法），給面板讓時鐘自己走。 */
        liveMs,
        timeParts,
        formatFull,
        refresh,
        travel: api.travel,
        shift: api.shift,
        reset: api.reset,
        confirmReset,
        openPanel,
        /** 狀態有變（stamp 不同）時通知：fn(新狀態, 舊狀態, 'local' | 'remote') */
        onChange(fn) { listeners.push(fn); },
    };

    function start() {
        buildUi();
        let initial = null;
        try {
            initial = script && script.dataset.state ? JSON.parse(script.dataset.state) : null;
        } catch {
            initial = null;
        }
        if (initial) setState(initial, 'initial');
        else refresh('initial');
    }

    if (document.body) start();
    else document.addEventListener('DOMContentLoaded', start);
})();
