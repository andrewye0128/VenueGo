// 字數計數：輸入時即時顯示「目前字數 / 上限」
// 用法：
//   <textarea maxlength="1500" data-char-counter="answerCount"></textarea>
//   <span id="answerCount"></span>          <!-- 顯示字數的位置 -->
// 上限取自 maxlength；接近上限（剩 10% 以內）時改成紅字提醒
(function () {
    document.querySelectorAll('[data-char-counter]').forEach(function (input) {
        let output = document.getElementById(input.dataset.charCounter);
        if (!output) return;

        let max = parseInt(input.getAttribute('maxlength'), 10);

        function update() {
            let length = input.value.length;
            output.textContent = length + ' / ' + max;
            output.classList.toggle('text-danger', length >= max * 0.9);
        }

        input.addEventListener('input', update);
        // 頁面載入時先算一次（編輯頁、驗證失敗退回時已經有內容）
        update();
    });
})();
