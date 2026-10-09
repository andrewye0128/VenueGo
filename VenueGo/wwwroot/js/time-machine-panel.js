/*
 * 時光機面板（/DevTools/Time）的畫面行為（昱）
 * 和伺服器溝通、「回到現在」確認視窗、通知其他分頁，都交給 time-machine.js（window.VenueGoTimeMachine）。
 *
 * 跳到指定時間的六個欄位：
 *   - 年：下拉選單，只有去年／今年／明年
 *   - 月 1–12、日 1–當月天數、時 0–23、分／秒 0–59
 *   - 只留數字：全形數字（１２）轉成半形，其他字元直接拿掉
 *   - 打完自動跳下一格：第一個數字「不可能再接第二個數字」時就跳，不必等兩位數
 *       月：0、1 會等第二個數字，2～9 直接跳
 *       日：0～3 會等，4～9 直接跳（2 月只有 0～2 會等，打 3 就直接跳）
 *       時：0～2 會等，3～9 直接跳
 *       分、秒：0～5 會等，6～9 直接跳（秒是最後一格，不跳）
 *     打了兩位數但不合理（例如月 13）就不跳，留在原地讓使用者改
 *   - 中文輸入法「組字中」先不處理，等字確定了才處理
 *     （10/7 版在組字途中就跳格，輸入法會把同一個數字也打進下一格）
 *   - 還沒打完的不算錯：例如月打了「0」還在等第二個數字，不會先標紅；但「出發」會先停用
 *   - 換了年或月，日超過當月天數時，自動改成當月最後一天並提示（例如 2/31 → 2/28）
 *     直接在「日」打了不存在的日期（例如 11 月打 31）則不改，標紅讓使用者自己改
 *   - 單擊只放游標、雙擊整格選取；Tab 鍵或自動跳格進來時整格選取
 *     整格已經有兩位數、又沒有選取時打字 → 改成從頭打（不然 maxlength 會擋住，打了沒反應）
 *   - 離開欄位時補成兩位數（7 → 07）
 *   - ↑↓ 鍵加減（超過範圍會繞回來）
 *   - 還沒動過的話，欄位會跟著網站時間一起走；一動就停住，按「↺ 填入目前網站時間」恢復
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

      /**
       * 只打了一個數字時，後面還能不能接第二個數字：「這個數字 × 10」沒超過上限就還能接。
       * 例如月（上限 12）打了 1 → 10 還在範圍內，要等；打了 2 → 20 超過，直接跳。
       * 日的上限跟著月份變，所以 2 月打了 3 → 30 超過 28，也會直接跳。
       */
      function canTakeSecondDigit(input, digit) {
            return Number(digit) * 10 <= Number(input.dataset.max);
      }
      const DEFAULT_HINT = el.hint.textContent;

      let dirty = false;   // 使用者動過欄位了，就不再自動跟著網站時間走
      let notice = null;   // 「日已改成 30」這類提示；使用者下一次輸入時清掉
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

      /** 全形數字轉半形，再拿掉所有不是數字的字元。 */
      function toDigits(text) {
            return String(text ?? '')
                  .replace(/[０-９]/g, (c) => String.fromCharCode(c.charCodeAt(0) - 0xfee0))
                  .replace(/\D/g, '');
      }

      function normalized(input) {
            return toDigits(input.value);
      }

      /**
       * 「還沒打完」：游標還在這格，而且是空的、或只打了一個「後面還能接第二個數字」的數字。
       * 例如月打了「0」——現在看是錯的，但使用者八成正要打「08」，先不要標紅。
       */
      function isPending(input) {
            if (document.activeElement !== input) return false;
            const digits = normalized(input);
            if (digits.length === 0) return true;
            if (digits.length === 1) return canTakeSecondDigit(input, digits);
            return false;
      }

      /** 讀成數字：空的或不是純數字都回 NaN。 */
      function readNumber(input) {
            const text = normalized(input);
            return /^\d+$/.test(text) ? Number(text) : NaN;
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

          if (normalized(input) === '') return `請填寫${label}`;
          if (Number.isNaN(value)) return `${label}只能填數字`;
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
          let anyError = false;
        for (const input of fields) {
            const error = checkField(input);
              if (error) anyError = true;
              // 還沒打完的那一格先不標紅、不顯示原因（「出發」還是會停用）
              const show = error && !isPending(input);
              input.setAttribute('aria-invalid', show ? 'true' : 'false');
              if (show && !firstError) { firstError = error; firstInvalid = input; }
        }
        if (firstError) {
            el.hint.textContent = firstError;
            el.hint.classList.add('error');
            } else {
                  // 沒有錯誤：顯示提示（如果有），不然回到一般說明
                  el.hint.classList.remove('error');
                  el.hint.textContent = notice ?? (dirty ? '已停住。按「出發」跳到這個時間。' : DEFAULT_HINT);
            }
            el.go.disabled = busy || anyError;
            return firstInvalid ?? fields.find((input) => checkField(input) !== null) ?? null;
      }

      /** 月是不是已經打完了（兩位數，或第一個數字就不可能再接）。 */
      function isMonthComplete() {
            const digits = normalized(byPart.month);
            return digits.length === 2 || (digits.length === 1 && !canTakeSecondDigit(byPart.month, digits));
      }

      /**
       * 換了年或月之後，日超過當月天數 → 改成當月最後一天，並在下面說一聲。
       * 只在「年或月改變」時做；使用者自己在日打了不存在的日期，不改，讓它標紅。
       */
      function clampDayAfterMonthChange() {
        updateDayMax();
            const month = readNumber(byPart.month);
            const day = readNumber(byPart.day);
            const max = Number(byPart.day.dataset.max);
            if (!isMonthComplete() || month < 1 || month > 12) return;
            if (Number.isNaN(day) || day <= max) return;

            byPart.day.value = pad(max);
            notice = `${el.year.value} 年 ${month} 月只有 ${max} 天，日已改成 ${max}。`;
            validateAll();
      }

      /** 跳到下一格並整格選取，直接打字就會取代原本的數字。 */
      function focusNext(index) {
            const next = fields[index + 1];
            if (!next) return;
            next.focus();
            next.select();
      }

      /** 這一格的字確定了（不是組字中）：整理成數字、檢查、決定要不要跳下一格。 */
      function handleTyped(input, index) {
            notice = null;
            const digits = normalized(input).slice(0, 2);
            if (digits !== input.value) input.value = digits;

            markDirty();
            validateAll();
            if (input.dataset.part === 'month') clampDayAfterMonthChange();

              const done = digits.length === 2
                    || (digits.length === 1 && !canTakeSecondDigit(input, digits));
              if (done && checkField(input) === null) focusNext(index);
        }

    fields.forEach((input, index) => {
              input.addEventListener('input', (event) => {
                    // 中文輸入法「組字中」先不處理，等 compositionend 再處理，
                    // 不然字還沒確定就跳格，輸入法會把同一個數字也打進下一格
                    if (event.isComposing) return;
                    handleTyped(input, index);
              });
              // 中文輸入法：在「已經有兩位數、又沒選取任何字」的格子裡組字時，maxlength 會把組出來的字擋掉，
              // 組字結束時格子裡還是原本的兩位數，就被當成「打完了」直接跳下一格。
              // 所以組字開始時先記下「要不要從頭打」，組字結束時用輸入法交出來的字（event.data）取代原本的內容。
              let restartOnCompose = false;
              input.addEventListener('compositionstart', () => {
                    restartOnCompose = normalized(input).length >= 2 && input.selectionStart === input.selectionEnd;
              });
              input.addEventListener('compositionend', (event) => {
                    if (restartOnCompose) {
                          const typed = toDigits(event.data);
                          if (typed) input.value = typed.slice(0, 2);
                          restartOnCompose = false;
                    }
                    handleTyped(input, index);
              });

              // 已經有兩位數、又沒有選取任何字時打字：改成從頭打。
              // 單擊只放游標（不全選），沒有這段的話 maxlength="2" 會把新打的字擋掉，看起來像打不進去
              input.addEventListener('beforeinput', (event) => {
                    if (event.isComposing || event.inputType !== 'insertText') return;
                    const typed = toDigits(event.data);
                    const full = normalized(input).length >= 2;
                    const nothingSelected = input.selectionStart === input.selectionEnd;
                    if (!typed || !full || !nothingSelected) return;
                    event.preventDefault();
                    input.value = typed.slice(0, 2);
                    handleTyped(input, index);
              });

        input.addEventListener('keydown', (event) => {
            if (event.key !== 'ArrowUp' && event.key !== 'ArrowDown') return;
              if (event.isComposing) return;
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
              notice = null;
              markDirty();
              validateAll();
              if (input.dataset.part === 'month') clampDayAfterMonthChange();
        });

          // 雙擊整格選取（瀏覽器本來雙擊就會選取整串數字，這裡確保一定是整格）
          input.addEventListener('dblclick', () => input.select());

          // 離開欄位時補成兩位數（7 → 07），再檢查一次（「還沒打完」的那格這時才會標紅）
          input.addEventListener('blur', () => {
                if (/^\d$/.test(input.value)) input.value = pad(Number(input.value));
                validateAll();
        });
    });

    el.year.addEventListener('change', () => {
          notice = null;
          markDirty();
          validateAll();
          clampDayAfterMonthChange();
    });

    el.refill.addEventListener('click', () => {
          dirty = false;
          notice = null;
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
