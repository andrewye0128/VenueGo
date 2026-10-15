// 最新消息新增／編輯頁：封面來源切換＋公版底稿編輯器（Fabric.js）
// 封面來源三選一：新增頁「分類預設圖／上傳圖片／公版底稿」，編輯頁「保留目前封面／上傳圖片／公版底稿」
// 公版底稿：底稿圖當背景，標題自動帶入分類中文名稱（不能編輯），內文直接在畫布上打字，反白後套用字級、顏色
// 送出時把內文、樣式 JSON、合成好的 PNG 放進隱藏欄位，由 ContentController 驗證後存檔
// 用法：
//   <div id="newsCoverSource"> 裡面放 <input type="radio" name="CoverSource" value="..."> </div>
//   <div data-cover-panel="上面 radio 的 value"> 各來源要顯示的區塊 </div>
//   公版底稿區塊用 _NewsCoverEditor.cshtml（畫布、字級／顏色按鈕、字數、提醒、縮圖預覽、3 個隱藏欄位）
//   編輯頁可加 <div id="newsCoverCategoryWarning" data-original-category="..."> 換分類時顯示的提醒 </div>
// 樣式 JSON 格式：[{ "start": 0, "end": 3, "color": "#1d5faf", "size": "lg" }]，只記有改過的片段
//   位置跟 Fabric.js 一樣：換行不算一個字
// 頁面要先載入 Fabric.js（全域變數 fabric）
(function () {
    let sourceBox = document.getElementById('newsCoverSource');
    if (!sourceBox) return;

    let form = sourceBox.closest('form');
    let panels = document.querySelectorAll('[data-cover-panel]');
    let fileInput = document.getElementById('ImageFile');
    let categorySelect = document.getElementById('Category');
    let canvasElement = document.getElementById('newsCoverCanvas');
    let charCount = document.getElementById('newsCoverCharCount');
    let messageBox = document.getElementById('newsCoverMessage');
    let hint = document.getElementById('newsCoverHint');
    let thumb = document.getElementById('newsCoverThumb');
    let textInput = document.getElementById('newsCoverTextInput');
    let stylesInput = document.getElementById('newsCoverStylesInput');
    let imageInput = document.getElementById('newsCoverImageInput');
    let categoryWarning = document.getElementById('newsCoverCategoryWarning');

    // 畫布實際大小（輸出封面用）與畫面上顯示的寬度
    const CANVAS_WIDTH = 1200;
    const CANVAS_HEIGHT = 1500;
    const DISPLAY_WIDTH = 400;
    // 底稿的區域（單位：畫布像素）：藍色色塊放標題、下方白色區域放內文，四周留白 PADDING
    const BAND_TOP = 213;
    const BAND_BOTTOM = 533;
    const BODY_TOP = 534;
    const PADDING = 60;
    const BODY_MAX_HEIGHT = CANVAS_HEIGHT - BODY_TOP - PADDING * 2;
    const FONT_FAMILY = '"Microsoft JhengHei", "PingFang TC", "Noto Sans TC", sans-serif';

    // 字數上限、字級、顏色都來自後端 NewsCoverOptions（畫布與按鈕的 data 屬性），這裡不另外寫一份
    // SIZE_PIXELS：字級代碼 >> 像素，例 { sm: 40, md: 48, lg: 56 }
    let SIZE_PIXELS = {};
    document.querySelectorAll('[data-cover-size]').forEach(function (button) {
        SIZE_PIXELS[button.dataset.coverSize] = Number(button.dataset.coverSizePx);
    });
    const MAX_LENGTH = canvasElement ? Number(canvasElement.dataset.maxLength) : 0;
    const DEFAULT_SIZE_CODE = canvasElement ? canvasElement.dataset.defaultSize : '';
    const DEFAULT_SIZE = SIZE_PIXELS[DEFAULT_SIZE_CODE];
    const DEFAULT_COLOR = canvasElement ? canvasElement.dataset.defaultColor : '';

    let canvas = null;
    let title = null;
    let body = null;
    let thumbTimer = null;

    // ===== 封面來源切換 =====

    function currentSource() {
        let checked = sourceBox.querySelector('input[name="CoverSource"]:checked');
        return checked ? checked.value : '';
    }

    function showPanel() {
        let source = currentSource();
        panels.forEach(function (panel) {
            panel.hidden = panel.dataset.coverPanel !== source;
        });

        // 不是「上傳圖片」時清掉已選的檔案，避免送出時一起上傳；觸發 change 讓 photo-preview.js 還原預覽
        if (source !== 'upload' && fileInput && fileInput.value) {
            fileInput.value = '';
            fileInput.dispatchEvent(new Event('change'));
        }

        // 畫布第一次顯示時才建立（隱藏中的元素量不到大小）
        if (source === 'template') initEditor();
    }

    // ===== 公版底稿編輯器 =====

    function initEditor() {
        if (canvas || !canvasElement) return;

        // 1. 建立畫布：實際 1200 x 1500，畫面上縮小顯示；已經是高解析度，不用再依螢幕倍率放大
        canvas = new fabric.Canvas(canvasElement, {
            width: CANVAS_WIDTH,
            height: CANVAS_HEIGHT,
            enableRetinaScaling: false,
            selection: false,
        });
        canvas.setDimensions(
            { width: DISPLAY_WIDTH + 'px', height: (DISPLAY_WIDTH * CANVAS_HEIGHT) / CANVAS_WIDTH + 'px' },
            { cssOnly: true }
        );

        // 2. 底稿圖當背景，寬度拉滿畫布
        fabric.FabricImage.fromURL(canvasElement.dataset.templateUrl).then(function (image) {
            image.scaleToWidth(CANVAS_WIDTH);
            canvas.backgroundImage = image;
            canvas.requestRenderAll();
            scheduleThumb();
        });

        // 3. 標題：放在藍色色塊正中央，白色粗體，不能點選
        title = new fabric.Textbox('', {
            left: CANVAS_WIDTH / 2,
            top: (BAND_TOP + BAND_BOTTOM) / 2,
            originX: 'center',
            originY: 'center',
            width: CANVAS_WIDTH - PADDING * 2,
            textAlign: 'center',
            fontSize: 120,
            fontWeight: 'bold',
            fontFamily: FONT_FAMILY,
            fill: '#ffffff',
            selectable: false,
            evented: false,
        });

        // 4. 內文：放在白色區域，固定位置與寬度，只能打字不能拖動或縮放；中文逐字換行
        body = new fabric.Textbox('', {
            left: PADDING,
            top: BODY_TOP + PADDING,
            originX: 'left',
            originY: 'top',
            width: CANVAS_WIDTH - PADDING * 2,
            fontSize: DEFAULT_SIZE,
            fontFamily: FONT_FAMILY,
            fill: DEFAULT_COLOR,
            lineHeight: 1.3,
            splitByGrapheme: true,
            lockMovementX: true,
            lockMovementY: true,
            hasControls: false,
            hoverCursor: 'text',
        });

        canvas.add(title, body);
        restoreInitialContent();

        // 5. 內文還沒有字時高度只有一行，點不太到；點白色區域任何地方都進入編輯，游標放到最後
        canvas.on('mouse:down', function (e) {
            if (e.target === body) return;
            if (canvas.getScenePoint(e.e).y < BODY_TOP) return;
            startEditingAtEnd();
        });

        body.on('changed', function () {
            updateStatus();
            scheduleThumb();
        });
        body.on('selection:changed', function () {
            hint.textContent = '';
        });

        categorySelect.addEventListener('change', updateTitle);
        updateTitle();
        updateStatus();
    }

    // 還原原本的內文與樣式（編輯頁原本是公版底稿、或資料有誤退回時）
    function restoreInitialContent() {
        let text = canvasElement.dataset.initialText || '';
        if (!text) return;

        let segments = [];
        try {
            segments = JSON.parse(canvasElement.dataset.initialStyles || '[]');
        } catch {
            segments = [];
        }

        // 樣式 JSON（顏色、字級代碼）>> Fabric 的格式（fill、fontSize 像素），再轉成 Fabric 內部的逐字樣式
        let fabricSegments = segments.map(function (segment) {
            return {
                start: segment.start,
                end: segment.end,
                style: { fill: segment.color, fontSize: SIZE_PIXELS[segment.size] || DEFAULT_SIZE },
            };
        });
        body.set('text', text);
        body.styles = fabric.util.stylesFromArray(fabricSegments, text);
        body.initDimensions();
        body.setCoords();
    }

    // Fabric 的逐字樣式 >> 樣式 JSON：只留跟預設不同的片段，相鄰且相同的片段合併
    function toStyleSegments() {
        let segments = [];
        fabric.util.stylesToArray(body.styles, body.text).forEach(function (item) {
            let color = item.style.fill || DEFAULT_COLOR;
            let size = sizeCodeOf(item.style.fontSize || DEFAULT_SIZE);
            if (color === DEFAULT_COLOR && size === DEFAULT_SIZE_CODE) return;

            let last = segments[segments.length - 1];
            if (last && last.end === item.start && last.color === color && last.size === size) {
                last.end = item.end;
                return;
            }
            segments.push({ start: item.start, end: item.end, color: color, size: size });
        });
        return segments;
    }

    // 像素 >> 字級代碼（找不到就當作預設字級）
    function sizeCodeOf(pixels) {
        let code = Object.keys(SIZE_PIXELS).find(function (key) {
            return SIZE_PIXELS[key] === pixels;
        });
        return code || DEFAULT_SIZE_CODE;
    }

    function startEditingAtEnd() {
        canvas.setActiveObject(body);
        body.enterEditing();
        body.setSelectionStart(body.text.length);
        body.setSelectionEnd(body.text.length);
        canvas.requestRenderAll();
    }

    // 標題跟著分類下拉選單變；還沒選分類時顯示半透明提示
    function updateTitle() {
        let name = categorySelect.value ? categorySelect.options[categorySelect.selectedIndex].text : '';
        title.set({ text: name || '請先選擇分類', opacity: name ? 1 : 0.5 });
        canvas.requestRenderAll();
        scheduleThumb();
    }

    // 字數與超出範圍提醒（只提醒；超過字數存檔時後端會擋下）
    function updateStatus() {
        let length = body.text.length;
        charCount.textContent = length + ' / ' + MAX_LENGTH;
        charCount.classList.toggle('text-danger', length > MAX_LENGTH);

        let messages = [];
        if (length > MAX_LENGTH) messages.push('內文超過 ' + MAX_LENGTH + ' 字，請刪減');
        if (body.height > BODY_MAX_HEIGHT) messages.push('內文超出白色區域，請刪減文字或調小字級');

        messageBox.replaceChildren();
        messages.forEach(function (message) {
            let box = document.createElement('div');
            box.className = 'alert alert-warning py-2 small mb-2';
            box.textContent = message;
            messageBox.appendChild(box);
        });
    }

    // 把字級或顏色套用到反白的文字
    function applyStyle(style) {
        if (!body || !body.isEditing || body.selectionStart === body.selectionEnd) {
            hint.textContent = '請先在畫布上反白要調整的文字';
            return;
        }

        body.setSelectionStyles(style, body.selectionStart, body.selectionEnd);
        // 字級變了要重新計算換行與高度
        body.initDimensions();
        body.setCoords();
        canvas.requestRenderAll();
        updateStatus();
        scheduleThumb();
    }

    // 縮圖預覽：停止操作 0.3 秒後才重畫，用 1/4 大小輸出
    function scheduleThumb() {
        clearTimeout(thumbTimer);
        thumbTimer = setTimeout(function () {
            thumb.src = canvas.toDataURL({ format: 'png', multiplier: 0.25 });
        }, 300);
    }

    // 按鈕按下時取消預設行為，畫布才不會失去焦點、反白也不會消失
    function keepTextSelection(button) {
        button.addEventListener('mousedown', function (e) {
            e.preventDefault();
        });
    }

    document.querySelectorAll('[data-cover-size]').forEach(function (button) {
        keepTextSelection(button);
        button.addEventListener('click', function () {
            applyStyle({ fontSize: SIZE_PIXELS[button.dataset.coverSize] });
        });
    });
    document.querySelectorAll('[data-cover-color]').forEach(function (button) {
        keepTextSelection(button);
        button.addEventListener('click', function () {
            applyStyle({ fill: button.dataset.coverColor });
        });
    });
    let clearButton = document.getElementById('newsCoverClearStyle');
    if (clearButton) {
        keepTextSelection(clearButton);
        clearButton.addEventListener('click', function () {
            applyStyle({ fontSize: DEFAULT_SIZE, fill: DEFAULT_COLOR });
        });
    }

    sourceBox.querySelectorAll('input[name="CoverSource"]').forEach(function (radio) {
        radio.addEventListener('change', showPanel);
    });

    // ===== 送出 =====
    // 選公版底稿 >> 填入內文、樣式 JSON、合成圖（1200 x 1500 PNG）；其他來源清空，後端不會使用
    if (form) {
        form.addEventListener('submit', function () {
            if (currentSource() === 'template' && body) {
                // 先離開編輯狀態、取消選取，輸出的圖才不會有游標或選取框
                if (body.isEditing) body.exitEditing();
                canvas.discardActiveObject();
                canvas.renderAll();

                textInput.value = body.text;
                stylesInput.value = JSON.stringify(toStyleSegments());
                imageInput.value = canvas.toDataURL({ format: 'png', multiplier: 1 });
            } else {
                textInput.value = '';
                stylesInput.value = '';
                imageInput.value = '';
            }
        });
    }

    // ===== 編輯頁：封面是公版底稿時換分類的提醒 =====
    // 分類名稱已經畫進封面圖，換分類後不會跟著變 >> 分類跟原本不同時顯示提醒
    if (categoryWarning) {
        let updateCategoryWarning = function () {
            categoryWarning.hidden = categorySelect.value === categoryWarning.dataset.originalCategory;
        };
        categorySelect.addEventListener('change', updateCategoryWarning);
        updateCategoryWarning();
    }

    // 頁面載入時依目前選的來源顯示（資料有誤退回時會保留原本的選擇）
    showPanel();
})();
