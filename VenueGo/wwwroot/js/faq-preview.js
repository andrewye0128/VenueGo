// 常見問題新增／編輯頁的預覽：模擬前台的樣子（問題當標題、回答在下面）
// 回答的連結解析在後端 FaqAnswerParser，這裡只呼叫 FaqPreview 拿解析結果再畫出來，規則只維護一份
// 用法（_FaqFormFields.cshtml）：
//   <div id="faqPreview" data-preview-url="/Content/FaqPreview">
//     <div id="faqPreviewQuestion"></div> <div id="faqPreviewAnswer"></div> <div id="faqPreviewMessages"></div>
//   </div>
// 安全：所有文字都用 textContent 放進畫面，不用 innerHTML，回答裡就算有 HTML 標籤也只會當成文字
(function () {
    let preview = document.getElementById('faqPreview');
    if (!preview) return;

    let form = preview.closest('form');
    let questionInput = document.getElementById('Question');
    let answerInput = document.getElementById('Answer');
    let questionOutput = document.getElementById('faqPreviewQuestion');
    let answerOutput = document.getElementById('faqPreviewAnswer');
    let messagesOutput = document.getElementById('faqPreviewMessages');

    // 停止輸入 0.5 秒後才呼叫後端，不會每打一個字就送一次
    const DELAY_MS = 500;
    let timer = null;
    // 記錄最新一次的請求編號：網路慢時，舊的回應比新的晚回來，要丟掉舊的
    let latestRequest = 0;

    // 問題不需要解析，直接顯示
    function renderQuestion() {
        let question = questionInput.value.trim();
        questionOutput.textContent = question || '（尚未輸入問題）';
        questionOutput.classList.toggle('text-muted', !question);
    }

    // 把文字放進畫面，換行改成 <br>
    function appendText(parent, text) {
        let lines = text.split('\n');
        lines.forEach(function (line, index) {
            if (index > 0) parent.appendChild(document.createElement('br'));
            parent.appendChild(document.createTextNode(line));
        });
    }

    function renderAnswer(segments) {
        answerOutput.replaceChildren();
        if (segments.length === 0) {
            let empty = document.createElement('span');
            empty.className = 'text-muted';
            empty.textContent = '（尚未輸入回答）';
            answerOutput.appendChild(empty);
            return;
        }

        segments.forEach(function (segment) {
            if (segment.type === 'link') {
                // 預覽裡的連結不能點（後台和前台是不同網站，也避免離開表單）；滑鼠移上去顯示完整網址
                let link = document.createElement('span');
                link.className = 'link-primary text-decoration-underline';
                link.title = segment.url + (segment.isExternal ? '（站外連結，前台會開新分頁）' : '（站內連結）');
                link.textContent = segment.text;
                answerOutput.appendChild(link);
            } else {
                appendText(answerOutput, segment.text);
            }
        });
    }

    // 錯誤（存檔會被擋下）紅色、提醒（不擋存檔）黃色
    function renderMessages(errors, warnings) {
        messagesOutput.replaceChildren();
        [
            { items: errors, css: 'alert-danger', title: '存檔前請修正' },
            { items: warnings, css: 'alert-warning', title: '提醒' },
        ].forEach(function (group) {
            if (group.items.length === 0) return;

            let box = document.createElement('div');
            box.className = 'alert ' + group.css + ' py-2 small mb-2';
            let title = document.createElement('div');
            title.className = 'fw-bold';
            title.textContent = group.title;
            box.appendChild(title);

            let list = document.createElement('ul');
            list.className = 'mb-0 ps-3';
            group.items.forEach(function (message) {
                let item = document.createElement('li');
                item.textContent = message;
                list.appendChild(item);
            });
            box.appendChild(list);
            messagesOutput.appendChild(box);
        });
    }

    async function refreshAnswer() {
        let requestId = ++latestRequest;
        let data = new FormData();
        data.append('answer', answerInput.value);
        // 帶上表單裡的防偽 token
        let token = form.querySelector('input[name="__RequestVerificationToken"]');
        if (token) data.append('__RequestVerificationToken', token.value);

        try {
            let response = await fetch(preview.dataset.previewUrl, { method: 'POST', body: data });
            let result = await response.json();
            if (requestId !== latestRequest) return;

            renderAnswer(result.data.segments);
            renderMessages(result.data.errors, result.data.warnings);
        } catch {
            if (requestId !== latestRequest) return;
            renderMessages(['預覽暫時無法載入，不影響存檔'], []);
        }
    }

    questionInput.addEventListener('input', renderQuestion);
    answerInput.addEventListener('input', function () {
        clearTimeout(timer);
        timer = setTimeout(refreshAnswer, DELAY_MS);
    });

    // 頁面載入時先畫一次（編輯頁、驗證失敗退回時已經有內容）
    renderQuestion();
    refreshAnswer();
})();
