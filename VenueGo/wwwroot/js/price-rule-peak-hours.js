//價格規則新增/編輯頁「每週尖峰時段」表格的畫面互動 >> SportTypePriceRuleCreate / SportTypePriceRuleEdit 共用
//表格本身在 Views/Venue/_SportTypePeakHoursTable.cshtml
//1. 每一列選了尖峰起始時間後,右邊即時顯示尖峰時段(例如「17:00–22:00,5 小時」)
//2. 7 天都選「不分尖峰/離峰」時,尖峰價格輸入框停用並清空、必填星號隱藏
//   (disabled 的欄位送出表單時不會夾帶值,後端收到 0,跟後端「7 天都沒有尖峰就強制清成 0」一致)
//3. 「全部不分尖峰/離峰」按鈕:把所有營業日清回不分尖峰/離峰(只改畫面,不會送出表單)
//這裡只是改善操作體驗,真正的把關在後端(Controller 的 NormalizePeakDays / ValidatePeakDays)
(function () {
    let selects = document.querySelectorAll('.peak-hour-select');
    let peakPriceInput = document.getElementById('PeakPrice');
    let peakPriceRequiredMark = document.getElementById('peakPriceRequiredMark');
    let peakPriceHint = document.getElementById('peakPriceHint');
    let clearAllButton = document.getElementById('clearAllPeakHours');

    //更新某一列右邊的尖峰時段說明,並依是否超出營業時間切換整列的橘色
    function updateEffect(select) {
        let row = select.closest('tr');
        let effect = row.querySelector('.peak-hour-effect');
        let value = select.value;
        let selectedOption = select.options[select.selectedIndex];

        if (value === '') {
            effect.textContent = '全天離峰價';
            effect.className = 'peak-hour-effect small text-muted';
            row.classList.remove('table-warning');
        }
        else if (selectedOption && selectedOption.dataset.out === '1') {
            //超出當天營業時間的舊值(只有編輯頁會出現),存檔時後端會擋下
            effect.textContent = '超出當天營業時間,存檔會被擋下,請改選其他時間或不分尖峰/離峰';
            effect.className = 'peak-hour-effect small text-warning-emphasis fw-medium';
            row.classList.add('table-warning');
        }
        else {
            //尖峰時段從選的時間一路到當天打烊,時數 = 打烊的小時 - 起始的小時
            let closeTime = select.dataset.close;
            let hours = parseInt(closeTime, 10) - parseInt(value, 10);
            effect.textContent = value + '–' + closeTime + '，' + hours + ' 小時';
            effect.className = 'peak-hour-effect small';
            row.classList.remove('table-warning');
        }
    }

    //是否有任何一天選了尖峰起始時間
    function hasAnyPeak() {
        for (let i = 0; i < selects.length; i++) {
            if (selects[i].value !== '') {
                return true;
            }
        }
        return false;
    }

    //依是否有尖峰,切換尖峰價格輸入框
    function togglePeakPrice() {
        if (!peakPriceInput) {
            return;
        }

        if (hasAnyPeak()) {
            peakPriceInput.disabled = false;
            peakPriceRequiredMark.style.display = '';
            peakPriceHint.textContent = '必須大於離峰價格';
        }
        else {
            peakPriceInput.disabled = true;
            //disable 的同時把殘留的舊數字清空,不然會留著一個灰掉卻還顯示數字的欄位,讓人誤以為那個值還有效
            peakPriceInput.value = '';
            //不需要填,星號跟著隱藏
            peakPriceRequiredMark.style.display = 'none';
            peakPriceHint.textContent = '7 天都不分尖峰/離峰,不需要填尖峰價格';
        }
    }

    //頁面載入時先依目前的值畫一次,再綁定 change 事件
    for (let i = 0; i < selects.length; i++) {
        updateEffect(selects[i]);
        selects[i].addEventListener('change', function () {
            updateEffect(this);
            togglePeakPrice();
        });
    }

    if (clearAllButton) {
        clearAllButton.addEventListener('click', function () {
            for (let i = 0; i < selects.length; i++) {
                selects[i].value = '';
                updateEffect(selects[i]);
            }
            togglePeakPrice();
        });
    }

    togglePeakPrice();
})();
