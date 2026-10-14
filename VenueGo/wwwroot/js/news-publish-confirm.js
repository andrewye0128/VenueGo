// 最新消息:送出前檢查發布時間,早於現在就跳出確認視窗,按「取消」就不送出
// 用法(新增、編輯頁共用):
//   <form data-server-now="2026-10-13T14:30:00" ...>        <!-- 開頁時的伺服器時間(ITimeService) -->
//   <form data-server-now="..." data-only-when-changed ...> <!-- 編輯頁:只有改過發布時間才檢查 -->
//   表單內要有 id="PublishedAt" 的 datetime-local 欄位
(function () {
    //容許的誤差(分鐘) >> 新增頁預設是現在,填寫期間時間會過去,不能一送出就被判定為過去時間
    const toleranceMinutes = 30;

    document.querySelectorAll('form[data-server-now]').forEach(function (form) {
        const input = form.querySelector('#PublishedAt');
        if (!input) return;

        //頁面打開時的伺服器時間,以及同一刻瀏覽器的時間
        //格式跟 datetime-local 一樣沒有時區,new Date() 會用相同方式解讀,兩邊才能直接比較
        const serverNowAtLoad = new Date(form.dataset.serverNow);
        const browserNowAtLoad = Date.now();

        //編輯頁:已發布的消息發布時間本來就是過去,沒改過就不檢查
        const onlyWhenChanged = form.hasAttribute('data-only-when-changed');

        form.addEventListener('submit', function (event) {
            //欄位還有錯誤時,交給前端驗證處理,不跳確認視窗
            if (window.jQuery && !jQuery(form).valid()) return;

            const value = input.value;
            if (!value) return;

            //defaultValue = 頁面載入時的值
            if (onlyWhenChanged && value === input.defaultValue) return;

            //現在 = 伺服器開頁時間 + 頁面打開後經過的時間
            //瀏覽器時鐘就算不準,兩次讀取的差值仍然正確,所以基準還是伺服器時間
            const elapsedMs = Date.now() - browserNowAtLoad;
            const serverNow = new Date(serverNowAtLoad.getTime() + elapsedMs);

            const publishedAt = new Date(value);
            const limit = new Date(serverNow.getTime() - toleranceMinutes * 60 * 1000);
            if (publishedAt < limit) {
                const message = '發布時間早於現在,這則消息會依該日期排序,可能不會出現在列表最前面。\n確定要送出嗎?';
                if (!confirm(message)) {
                    event.preventDefault();
                }
            }
        });
    });
})();
