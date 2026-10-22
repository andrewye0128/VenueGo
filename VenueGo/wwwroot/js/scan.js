(function () {
    "use strict";

    const app = document.getElementById("scanApp");
    if (!app) return;

    // ───────── 設定與元素 ─────────
    const VERIFY_URL = app.dataset.verifyUrl;
    const VENUE_ID = app.dataset.venueId;
    const PAUSE_MS = Number(app.dataset.pauseMs) || 2500;   // 掃到一張後，暫停多久不接受新的掃描
    const SAME_QR_IGNORE_MS = 10000;                        // 同一個 QR 還在鏡頭前時，這段時間內不重複送出
    const MAX_HISTORY_ROWS = 20;
    const antiforgeryToken = app.querySelector('input[name="__RequestVerificationToken"]').value;

    const video = document.getElementById("scanVideo");
    const cameraSelect = document.getElementById("cameraSelect");
    const btnPause = document.getElementById("btnPause");
    const cameraStatus = document.getElementById("cameraStatus");
    const resultCard = document.getElementById("resultCard");
    const resultIcon = document.getElementById("resultIcon");
    const resultTitle = document.getElementById("resultTitle");
    const resultBody = document.getElementById("resultBody");
    const resultAction = document.getElementById("resultAction");
    const historyBody = document.getElementById("historyBody");

    // 結果卡片各種狀態的外框顏色和圖示
    const RESULT_STYLES = {
        idle: { border: "border-secondary", icon: "bi-qr-code-scan text-secondary" },
        loading: { border: "border-secondary", icon: "bi-hourglass-split text-secondary" },
        success: { border: "border-success", icon: "bi-check-circle-fill text-success" },
        fail: { border: "border-danger", icon: "bi-x-circle-fill text-danger" },
        warning: { border: "border-warning", icon: "bi-exclamation-triangle-fill text-warning" }
    };

    // ───────── 狀態 ─────────
    let scannerControls = null;   // zxing 回傳的控制器，用來停止掃描
    let paused = false;           // 櫃台按了「暫停掃描」
    let busy = false;             // 正在驗證，或剛掃完還在暫停時間內
    let lastText = "";            // 上一個掃到的 QR 內容
    let lastScanAt = 0;           // 上一次掃到的時間

    // ───────── 小工具 ─────────

    // 建立元素。文字一律用 textContent，不用 innerHTML，避免會員姓名等資料被當成 HTML 執行
    function el(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    function currentMode() {
        return document.querySelector('input[name="scanMode"]:checked').value;   // "in" 或 "out"
    }

    function modeText(mode) {
        return mode === "in" ? "入場" : "離場";
    }

    function setStatus(text, isError) {
        cameraStatus.textContent = text;
        cameraStatus.classList.toggle("text-danger", !!isError);
        cameraStatus.classList.toggle("text-muted", !isError);
    }

        // ───────── 結果卡片 ─────────

    function showResultCard(kind, title) {
        const style = RESULT_STYLES[kind];

        Object.values(RESULT_STYLES).forEach(s => resultCard.classList.remove(s.border));
        resultCard.classList.add(style.border);

        resultIcon.replaceChildren(el("i", "bi " + style.icon));
        resultTitle.textContent = title;
    }

    // 票券資料：#票號 · 會員 / 場館 / 日期 時段
    function showSummary(summary) {
        resultBody.replaceChildren();
        resultBody.classList.remove("text-muted");

        if (!summary) {
            resultBody.classList.add("text-muted");
            resultBody.textContent = "無票券資料";
            return;
        }

        resultBody.append(
            el("div", "fs-5 fw-semibold", "#" + summary.ticketId + " · " + summary.memberName),
            el("div", "", summary.venueName),
            el("div", "text-muted", summary.date + "  " + summary.timeRange)
        );
    }

         function showIdle() {
        showResultCard("idle", "等待掃描");
        resultBody.classList.add("text-muted");
        resultBody.textContent = "掃描結果會顯示在這裡";
        resultAction.classList.add("d-none");
    }

    function showLoading() {
        showResultCard("loading", "驗證中…");
        resultBody.replaceChildren();
        resultAction.classList.add("d-none");
    }

    function showVerifyResult(result) {
        let kind = "fail";
        if (result.success) kind = "success";
        else if (result.needOvertimeCheckout) kind = "warning";

        showResultCard(kind, result.message);
        showSummary(result.ticketSummary);

        // 離場超時：顯示「前往結帳」
        if (result.needOvertimeCheckout && result.overtimeCheckoutUrl) {
            resultAction.href = result.overtimeCheckoutUrl;
            resultAction.classList.remove("d-none");
        } else {
            resultAction.classList.add("d-none");
        }
    }

    function showSystemError(message) {
        showResultCard("fail", message);
        resultBody.replaceChildren();
        resultAction.classList.add("d-none");
    }

        // ───────── 最近掃描紀錄（只在這個畫面上，重新整理就清空） ─────────

    function addHistory(mode, result) {
        const empty = document.getElementById("historyEmpty");
        if (empty) empty.remove();

        const summary = result.ticketSummary;
        let badgeClass = "bg-danger";
        if (result.success) badgeClass = "bg-success";
        else if (result.needOvertimeCheckout) badgeClass = "bg-warning text-dark";

        const row = el("tr");
        row.append(
            el("td", "", new Date().toLocaleTimeString("zh-TW", { hour12: false })),
            el("td", "", modeText(mode)),
            el("td", "", summary ? "#" + summary.ticketId : "—"),
            el("td", "", summary ? summary.memberName : "—")
        );

        const resultCell = el("td");
        resultCell.append(el("span", "badge " + badgeClass, result.message));
        row.append(resultCell);

        historyBody.prepend(row);

        while (historyBody.rows.length > MAX_HISTORY_ROWS) {
            historyBody.deleteRow(historyBody.rows.length - 1);
        }
    }

    // ───────── 送後端驗票 ─────────

    async function postVerify(payload, mode) {
        const response = await fetch(VERIFY_URL, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": antiforgeryToken,   // 後端 [ValidateAntiForgeryToken] 預設讀這個標頭
                "X-Requested-With": "XMLHttpRequest"
            },
            body: JSON.stringify({ payload: payload, mode: mode, venueId: VENUE_ID })
        });

        // 登入逾期時，後端會導向登入頁（回傳的是 HTML 不是 JSON）
        const isJson = (response.headers.get("content-type") || "").includes("application/json");
        if (response.redirected || response.status === 401 || !isJson) {
            throw new Error("登入已逾期，請重新整理頁面並重新登入");
        }

        const data = await response.json();
        if (!response.ok) {
            // 400 等：後端用 ApiResult 回 { message }
            throw new Error(data.message || "系統發生錯誤，請稍後再試");
        }
        return data;
    }

         async function handleScan(payload) {
        busy = true;
        lastText = payload;
        lastScanAt = Date.now();

        const mode = currentMode();
        showLoading();

        try {
            const result = await postVerify(payload, mode);
            showVerifyResult(result);
            addHistory(mode, result);
        } catch (err) {
            showSystemError(err.message || "系統發生錯誤，請稍後再試");
        } finally {
            // 暫停一下，讓櫃台看清楚結果，也避免同一張連續送出
            setTimeout(function () { busy = false; }, PAUSE_MS);
        }
    }

    // zxing 每次掃描嘗試都會呼叫：沒掃到 QR 時 result 是空的、error 是「找不到」，這是正常情況，直接略過
    function onScan(result) {
        if (!result || paused || busy) return;

        const text = result.getText();
        // 同一個 QR 還舉在鏡頭前，不重複送出
        if (text === lastText && Date.now() - lastScanAt < SAME_QR_IGNORE_MS) return;

        handleScan(text);
    }

             // ───────── 鏡頭 ─────────

    function cameraErrorText(err) {
        if (err && err.name === "NotAllowedError") return "鏡頭權限被拒絕，請在網址列允許使用鏡頭後重新整理頁面";
        if (err && err.name === "NotFoundError") return "找不到鏡頭，請確認裝置已連接";
        if (err && err.name === "NotReadableError") return "鏡頭正被其他程式使用中，請關閉後重新整理頁面";
        return "無法開啟鏡頭：" + (err && err.message ? err.message : err);
    }

    function stopCamera() {
        if (scannerControls) {
            scannerControls.stop();
            scannerControls = null;
        }
    }

    // 開始掃描。deviceId 沒給就用預設鏡頭
    async function startCamera(deviceId) {
        stopCamera();
        setStatus("正在開啟鏡頭…", false);

        try {
            const reader = new ZXingBrowser.BrowserQRCodeReader();
            scannerControls = await reader.decodeFromVideoDevice(deviceId || undefined, video, onScan);
            setStatus(paused ? "已暫停掃描" : "鏡頭已開啟，請將 QR Code 對準畫面", false);
            await loadCameraList();
        } catch (err) {
            setStatus(cameraErrorText(err), true);
        }
    }

    // 填鏡頭下拉選單。要等使用者允許鏡頭權限之後，才拿得到鏡頭名稱，所以放在開啟鏡頭之後
    async function loadCameraList() {
        const devices = await ZXingBrowser.BrowserCodeReader.listVideoInputDevices();

        // 目前正在使用的鏡頭
        const track = video.srcObject && video.srcObject.getVideoTracks()[0];
        const currentId = track ? track.getSettings().deviceId : "";

        cameraSelect.replaceChildren();
        devices.forEach(function (device) {
            const option = el("option", "", device.label);
            option.value = device.deviceId;
            option.selected = device.deviceId === currentId;
            cameraSelect.append(option);
        });
    }

    // ───────── 事件 ─────────

    cameraSelect.addEventListener("change", function () {
        startCamera(cameraSelect.value);
    });

    btnPause.addEventListener("click", function () {
        paused = !paused;
        btnPause.textContent = paused ? "繼續掃描" : "暫停掃描";
        setStatus(paused ? "已暫停掃描" : "鏡頭已開啟，請將 QR Code 對準畫面", false);
    });

    // 離開頁面時關掉鏡頭，鏡頭燈才會熄
    window.addEventListener("pagehide", stopCamera);

    // ───────── 啟動 ─────────

    function init() {
        showIdle();

        if (!window.ZXingBrowser) {
            setStatus("掃描套件載入失敗，請重新整理頁面", true);
            return;
        }
        // 瀏覽器規定：只有 https 或 localhost 才能開鏡頭
        if (!window.isSecureContext || !navigator.mediaDevices) {
            setStatus("此網址無法使用鏡頭，請改用 https 或 localhost 開啟", true);
            return;
        }

        startCamera();
    }

     init();
})();