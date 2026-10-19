// 最新消息:發布時間的即時摘要＋送出前確認
// 1. 即時摘要:瀏覽器的日期選擇器沒有確定按鈕,選完時間後在欄位下方顯示設定結果(排程中／立即顯示／早於現在／草稿)
// 2. 送出前確認:發布時間早於現在,跳出確認視窗,按「取消」就不送出
// 用法(新增、編輯頁共用):
//   <form data-server-now="2026-10-13T14:30:00" ...>        <!-- 開頁時的伺服器時間(ITimeService) -->
//   <form data-server-now="..." data-only-when-changed ...> <!-- 編輯頁:只有改過發布時間才檢查 -->
//   表單內要有 id="PublishedAt" 的 datetime-local 欄位;
//   選填:id="publishedAtSummary" 的摘要區塊、id="IsPublished" 的上架開關
(function () {
    //容許的誤差(分鐘) >> 新增頁預設是現在,填寫期間時間會過去,不能一送出就被判定為過去時間
    const toleranceMinutes = 30;
    const weekDays = ['日', '一', '二', '三', '四', '五', '六'];

    document.querySelectorAll('form[data-server-now]').forEach(function (form) {
        const input = form.querySelector('#PublishedAt');
        if (!input) return;
        const summary = form.querySelector('#publishedAtSummary');
        const publishSwitch = form.querySelector('#IsPublished');

        //頁面打開時的伺服器時間,以及同一刻瀏覽器的時間
        //格式跟 datetime-local 一樣沒有時區,new Date() 會用相同方式解讀,兩邊才能直接比較
        const serverNowAtLoad = new Date(form.dataset.serverNow);
        const browserNowAtLoad = Date.now();

        //編輯頁:已發布的消息發布時間本來就是過去,沒改過就不檢查
        const onlyWhenChanged = form.hasAttribute('data-only-when-changed');

        //現在 = 伺服器開頁時間 + 頁面打開後經過的時間
        //瀏覽器時鐘就算不準,兩次讀取的差值仍然正確,所以基準還是伺服器時間
        function getServerNow() {
            const elapsedMs = Date.now() - browserNowAtLoad;
            return new Date(serverNowAtLoad.getTime() + elapsedMs);
        }

        //發布時間是否早於現在超過容許誤差(要跳確認視窗的情況)
        function isTooEarly(publishedAt) {
            const limit = new Date(getServerNow().getTime() - toleranceMinutes * 60 * 1000);
            return publishedAt < limit;
        }

        //編輯頁沒改過發布時間 >> 不需要確認(defaultValue = 頁面載入時的值)
        function isUnchanged() {
            return onlyWhenChanged && input.value === input.defaultValue;
        }

        //例:2026/10/16（五）下午 2:30
        //12 小時制:0 點顯示「上午 12」、12 點顯示「下午 12」;小時不補零,分鐘補零
        function formatDateTime(date) {
            const pad = function (n) { return String(n).padStart(2, '0'); };
            const hours = date.getHours();
            const period = hours < 12 ? '上午' : '下午';
            const hour12 = hours % 12 === 0 ? 12 : hours % 12;
            return date.getFullYear() + '/' + pad(date.getMonth() + 1) + '/' + pad(date.getDate())
                + '（' + weekDays[date.getDay()] + '）' + period + ' ' + hour12 + ':' + pad(date.getMinutes());
        }

        //===== 即時摘要 =====
        //依「有沒有上架」與「發布時間跟現在的關係」決定文字與顏色
        function renderSummary() {
            if (!summary) return;

            if (!input.value) {
                showSummary('text-danger', 'bi-exclamation-circle', '尚未選擇發布時間');
                return;
            }

            const publishedAt = new Date(input.value);
            const timeText = formatDateTime(publishedAt);

            //1. 沒上架 >> 草稿,時間只影響之後上架時的排序
            if (publishSwitch && !publishSwitch.checked) {
                showSummary('text-secondary', 'bi-file-earmark', '發布時間 ' + timeText + ';目前尚未上架');
                return;
            }

            //2. 未來時間 >> 排程發布
            if (publishedAt > getServerNow()) {
                showSummary('text-primary', 'bi-clock', '將於 ' + timeText + ' 發布');
                return;
            }

            //3. 早於現在太多 >> 補發或編輯過去的消息,送出時會跳確認視窗
            if (isTooEarly(publishedAt) && !isUnchanged()) {
                showSummary('text-warning-emphasis', 'bi-exclamation-triangle', '補發或編輯過去消息,消息刊登時間為 ' + timeText);
                return;
            }

            //4. 現在或稍早 >> 送出後立即刊登
            showSummary('text-success', 'bi-check-circle', '確認發布後立即刊登');
        }

        //用 textContent 放文字,不用 innerHTML
        function showSummary(colorClass, iconClass, text) {
            summary.className = 'small mt-1 ' + colorClass;
            summary.replaceChildren();
            const icon = document.createElement('i');
            icon.className = 'bi ' + iconClass + ' me-1';
            icon.setAttribute('aria-hidden', 'true');
            summary.appendChild(icon);
            summary.appendChild(document.createTextNode(text));
        }

        //input:選擇器每次改動都會觸發;change:部分瀏覽器關閉選擇器時才觸發,兩個都聽
        input.addEventListener('input', renderSummary);
        input.addEventListener('change', renderSummary);
        if (publishSwitch) publishSwitch.addEventListener('change', renderSummary);
        renderSummary();

        //===== 送出前確認 =====
        form.addEventListener('submit', function (event) {
            //欄位還有錯誤時,交給前端驗證處理,不跳確認視窗
            if (window.jQuery && !jQuery(form).valid()) return;

            if (!input.value) return;
            if (isUnchanged()) return;

            if (isTooEarly(new Date(input.value))) {
                const message = '發布時間早於現在,這則消息會依該日期排序,可能不會出現在列表最前面。\n確定要送出嗎?';
                if (!confirm(message)) {
                    event.preventDefault();
                }
            }
        });
    });
})();
