/*
 * 時光機面板（/DevTools/Time）的畫面行為（昱）
 * 和伺服器溝通、「回到現在」確認視窗、通知其他分頁，都交給 time-machine.js（window.VenueGoTimeMachine）。
 *
 * 跳到指定時間的六個欄位：
 *   - 年：下拉選單，只有去年／今年／明年
 *   - 月 1–12、日 1–當月天數、時 0–23、分／秒 0–59
 *   - 只能打數字；打滿兩位數自動跳到下一格；↑↓ 鍵加減（超過範圍會繞回來）
 *   - 還沒動過的話，欄位會跟著網站時間一起走；一動就停住，按「↺ 填入目前網站時間」恢復
 *   - 換了年或月，日超過當月天數時自動改成最後一天，並提示
 */
(function () {
    'use strict';

    const tm = window.VenueGoTimeMachine;
    if (!tm) return;

    const script = document.currentScript;
    const $ = (id) => document.getElementById(id);

    const el = {
        now: $('tmNow'), siteNow: $('tmSiteNow'), realNow: $('tmRealNow'), status: $('tmStatus'),
        form: $('tmJumpForm'), year: $('tmYear'), hint: $('tmHint'), refill: $('tmRefill'), go: $('tmGo'),
        resetCard: $('tmResetCard'), reset: $('tmReset'),
    };
    const fields = ['tmMonth', 'tmDay', 'tmHour', 'tmMinute', 'tmSecond'].map($);
    const byPart = Object.fromEntries(fields.map((f) => [f.dataset.part, f]));
    const LABELS = { month: '月', day: '日', hour: '時', minute: '分', second: '秒' };
    const DEFAULT_HINT = el.hint.textContent;

    let dirty = false;   // 使用者動過欄位了，就不再自動跟著網站時間走
    let busy = false;

    const pad = (n) => String(n).padStart(2, '0');
    const daysInMonth = (year, month) => new Date(Date.UTC(year, month, 0)).getUTCDate();

    // ═══════════════════════════════════════════════════
    //  上方的時鐘
    // ═══════════════════════════════════════════════════

    function renderNow() {
        const state = tm.state;
        if (!state) return;

        el.siteNow.textContent = tm.formatFull(tm.liveMs(state.siteNow));
        el.realNow.innerHTML = '';
        el.realNow.append(`真實時間 ${tm.formatFull(tm.liveMs(state.realNow))}`);
        if (state.traveling) {
            const strong = document.createElement('strong');
            strong.textContent = `・比真實時間${state.offsetText}`;
            el.realNow.append(strong);
        }
        el.now.classList.toggle('traveling', state.traveling);
        el.resetCard.hidden = !state.traveling;

        const focused = fields.includes(document.activeElement) || document.activeElement === el.year;
        if (!dirty && !focused) fillFromSite();
    }

    // ═══════════════════════════════════════════════════
    //  六個欄位
    // ═══════════════════════════════════════════════════

    function fill(p) {
        ensureYearOption(p.year);
        el.year.value = String(p.year);
        byPart.month.value = pad(p.month);
        byPart.day.value = pad(p.day);
        byPart.hour.value = pad(p.hour);
        byPart.minute.value = pad(p.minute);
        byPart.second.value = pad(p.second);
        updateDayMax();
    }

    function fillFromSite() {
        if (!tm.state) return;
        fill(tm.timeParts(tm.liveMs(tm.state.siteNow)));
    }

    /** 網站時間跑到選單沒有的年份（例如連按好幾次 ＋7 天跨年）時，補上那一年。 */
    function ensureYearOption(year) {
        if ([...el.year.options].some((o) => o.value === String(year))) return;
        const option = new Option(String(year), String(year));
        const after = [...el.year.options].find((o) => Number(o.value) > year);
        el.year.add(option, after || null);
    }

    function markDirty() {
        if (dirty) return;
        dirty = true;
        el.hint.classList.remove('error');
        el.hint.textContent = '已停住。按「出發」跳到這個時間。';
    }

    function readNumber(input) {
        return input.value === '' ? NaN : Number(input.value);
    }

    function updateDayMax() {
        const year = Number(el.year.value);
        const month = readNumber(byPart.month);
        byPart.day.dataset.max = month >= 1 && month <= 12 ? String(daysInMonth(year, month)) : '31';
    }

    /** 檢查一格。回傳錯誤訊息，沒錯回 null。 */
    function checkField(input) {
        const value = readNumber(input);
        const label = LABELS[input.dataset.part];
        const min = Number(input.dataset.min);
        const max = Number(input.dataset.max);

        if (Number.isNaN(value)) return `請填寫${label}`;
        if (value < min || value > max) {
            if (input.dataset.part === 'day' && value >= 1 && value <= 31) {
                return `${el.year.value} 年 ${readNumber(byPart.month)} 月只有 ${max} 天`;
            }
            return `${label}要在 ${min}～${max} 之間`;
        }
        return null;
    }

    /** 檢查全部。有錯就標紅、把第一個錯誤寫在下面。 */
    function validateAll() {
        updateDayMax();
        let firstError = null;
        let firstInvalid = null;
        for (const input of fields) {
            const error = checkField(input);
            input.setAttribute('aria-invalid', error ? 'true' : 'false');
            if (error && !firstError) { firstError = error; firstInvalid = input; }
        }
        if (firstError) {
            el.hint.textContent = firstError;
            el.hint.classList.add('error');
        } else if (el.hint.classList.contains('error')) {
            el.hint.classList.remove('error');
            el.hint.textContent = dirty ? '已停住。按「出發」跳到這個時間。' : DEFAULT_HINT;
        }
        el.go.disabled = busy || !!firstError;
        return firstInvalid;
    }

    /** 換了年或月：日超過當月天數就改成最後一天，並說一聲。 */
    function clampDay() {
        updateDayMax();
        const day = readNumber(byPart.day);
        const max = Number(byPart.day.dataset.max);
        if (!Number.isNaN(day) && day > max) {
            byPart.day.value = pad(max);
            validateAll();
            el.hint.classList.remove('error');
            el.hint.textContent = `${el.year.value} 年 ${readNumber(byPart.month)} 月只有 ${max} 天，已改成 ${max} 日。`;
            return;
        }
        validateAll();
    }

    fields.forEach((input, index) => {
        input.addEventListener('input', () => {
            const digits = input.value.replace(/\D/g, '').slice(0, 2);
            if (digits !== input.value) input.value = digits;
            markDirty();

            if (input.dataset.part === 'month' && digits.length === 2) clampDay();
            else validateAll();

            // 打滿兩位數就跳下一格（最後一格就停在原地）
            if (digits.length === 2 && index < fields.length - 1) {
                fields[index + 1].focus();
                fields[index + 1].select();
            }
        });

        input.addEventListener('keydown', (event) => {
            if (event.key !== 'ArrowUp' && event.key !== 'ArrowDown') return;
            event.preventDefault();
            updateDayMax();
            const min = Number(input.dataset.min);
            const max = Number(input.dataset.max);
            let value = readNumber(input);
            if (Number.isNaN(value)) value = min;
            else value += event.key === 'ArrowUp' ? 1 : -1;
            if (value > max) value = min;   // 超過範圍就繞回來，例如 59 → 00
            if (value < min) value = max;
            input.value = pad(value);
            markDirty();
            if (input.dataset.part === 'month') clampDay();
            else validateAll();
        });

        input.addEventListener('focus', () => input.select());

        // 離開欄位時補成兩位數（7 → 07）
        input.addEventListener('blur', () => {
            if (/^\d$/.test(input.value)) input.value = pad(Number(input.value));
            if (input.dataset.part === 'month') clampDay();
            else validateAll();
        });
    });

    el.year.addEventListener('change', () => {
        markDirty();
        clampDay();
    });

    el.refill.addEventListener('click', () => {
        dirty = false;
        fillFromSite();
        validateAll();
        el.hint.classList.remove('error');
        el.hint.textContent = DEFAULT_HINT;
    });

    // ═══════════════════════════════════════════════════
    //  按鈕
    // ═══════════════════════════════════════════════════

    function setBusy(value) {
        busy = value;
        document.querySelectorAll('[data-shift], #tmReset').forEach((b) => { b.disabled = value; });
        validateAll();
    }

    function showStatus(message, ok) {
        el.status.textContent = message || '';
        el.status.className = 'status ' + (ok ? 'ok' : 'error');
    }

    /** 送出一個操作：處理忙碌狀態和訊息。 */
    async function run(action) {
        if (busy) return;
        setBusy(true);
        try {
            const json = await action();
            showStatus(json.message, true);
            return json;
        } catch (e) {
            // try/catch 的決定：失敗時把伺服器給的原因顯示在上方，欄位保持原樣讓使用者修正
            showStatus(e.message || '操作失敗，請再試一次', false);
            return null;
        } finally {
            setBusy(false);
        }
    }

    document.querySelectorAll('[data-shift]').forEach((button) => {
        button.addEventListener('click', () => run(() => tm.shift(Number(button.dataset.shift))));
    });

    el.form.addEventListener('submit', async (event) => {
        event.preventDefault();
        const invalid = validateAll();
        if (invalid) { invalid.focus(); return; }

        const parts = {
            year: Number(el.year.value),
            month: readNumber(byPart.month),
            day: readNumber(byPart.day),
            hour: readNumber(byPart.hour),
            minute: readNumber(byPart.minute),
            second: readNumber(byPart.second),
        };
        const json = await run(() => tm.travel(parts));
        if (json) {
            dirty = false;   // 跳過去之後，欄位繼續跟著（新的）網站時間走
            el.hint.textContent = DEFAULT_HINT;
            renderNow();
        }
    });

    el.reset.addEventListener('click', () => tm.confirmReset());

    tm.onChange((next, previous, source) => {
        if (source === 'remote') showStatus('其他視窗調整了時間，這裡已經更新。', true);
        if (source === 'local' && previous.traveling && !next.traveling) showStatus('已回到現在。', true);
        if (!dirty) fillFromSite();
        renderNow();
    });

    // ═══════════════════════════════════════════════════
    //  開始
    // ═══════════════════════════════════════════════════

    // 網址帶了 ?to=2026-10-30T16:45 → 先填進去（不會自動出發）
    const prefill = script && script.dataset.prefill;
    const match = prefill && /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})$/.exec(prefill);
    if (match) {
        fill({ year: +match[1], month: +match[2], day: +match[3], hour: +match[4], minute: +match[5], second: +match[6] });
        dirty = true;
        el.hint.textContent = '已從網址填入。按「出發」跳到這個時間。';
    } else {
        fillFromSite();
    }

    renderNow();
    validateAll();
    setInterval(renderNow, 1000);
})();
