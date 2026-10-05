/* ============================================================
   terminate-modal.js — 取消／作廢預約彈窗（_TerminateReservationModal）
   放置路徑：wwwroot/js/terminate-modal.js

   這個彈窗元件同時被預約詳細頁（Views/Reservation/Details.cshtml）跟
   預約列表頁（Views/Reservation/Index.cshtml）使用，獨立成專屬的 JS 檔案，
   兩邊各自在自己的 @section Scripts 引用這一份，不寫進全站共用的 site.js：
     1. 只跟這個彈窗元件有關的邏輯，不屬於「大家共用的雜項工具」。
     2. site.js 是任何人都可能會改的共用檔案，獨立出來能降低跟其他組員的
        Git 衝突機率，之後要找這段邏輯也比翻 site.js 直覺。
   ============================================================ */

(function () {
    'use strict';

    // ── 原因欄位字數計數器 ──────────────────────────────
    // 上限 200 對應 Reservations.CancelReason 的 nvarchar(200)，
    // textarea 的 maxlength 已經擋住超出的輸入，這裡只負責顯示。
    //
    // ⚠️ 用事件委派掛在 document 上，不要在頁面載入時用 querySelectorAll
    //    逐一綁定：預約列表頁的表格（含裡面的取消彈窗）之後會被非同步整塊換掉，
    //    換掉之後新長出來的 textarea 不會是「頁面載入當下」就存在的元素，
    //    逐一綁定的寫法只會在第一次有效。掛在 document 上就不受這個限制。
    document.addEventListener('input', function (e) {
        if (e.target.tagName !== 'TEXTAREA') return;

        const counter = document.querySelector('.reason-counter[data-target="' + e.target.id + '"]');
        if (!counter) return;

        counter.textContent = e.target.value.length;
    });

})();
