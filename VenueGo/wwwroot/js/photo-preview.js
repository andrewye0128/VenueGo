// 用法：
//   <input type="file" data-preview-target="photoPreview" ... />
//   <img id="photoPreview" hidden />              <!-- 預覽圖 -->
//   <p id="photoPreviewEmptyText">尚未上傳照片</p>  <!-- 選填：無照片時的提示文字 -->
//
// 選檔後用 FileReader 讀成 data URL 顯示在對應 id 的 <img> 上；
// 取消選擇時會還原成頁面載入當下的原始狀態（新增頁沒有照片就還原成隱藏）。
(function () {
    document.querySelectorAll('input[type="file"][data-preview-target]').forEach(function (input) {
        let preview = document.getElementById(input.dataset.previewTarget);
        if (!preview) return;

        let emptyText = document.getElementById(input.dataset.previewTarget + 'EmptyText');
        let originalSrc = preview.getAttribute('src') || '';
        let hadOriginalPhoto = !preview.hidden;

        input.addEventListener('change', function () {
            let file = input.files && input.files[0];

            if (!file) {
                // 使用者清除選擇 >> 還原成頁面載入時的原始畫面
                preview.src = originalSrc;
                preview.hidden = !hadOriginalPhoto;
                if (emptyText) emptyText.hidden = hadOriginalPhoto;
                return;
            }

            let reader = new FileReader();
            reader.onload = function (e) {
                preview.src = e.target.result;
                preview.hidden = false;
                if (emptyText) emptyText.hidden = true;
            };
            reader.readAsDataURL(file);
        });
    });
})();
