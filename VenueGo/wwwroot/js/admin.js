// 放置路徑：wwwroot/js/admin.js
// 後台版型互動：側欄收合 / 手機版滑出 / 全螢幕

(function () {
    'use strict';

    const layout = document.getElementById('adminLayout');
    if (!layout) return;

    const isMobile = () => window.matchMedia('(max-width: 991.98px)').matches;

    // 還原上次的收合狀態
    if (localStorage.getItem('venuego-sidebar') === 'collapsed') {
        layout.classList.add('is-collapsed');
    }

    // Header 的漢堡鈕：桌機收合、手機滑出
    document.getElementById('sidebarToggle')?.addEventListener('click', function () {
        if (isMobile()) {
            layout.classList.toggle('is-open');
        } else {
            toggleCollapse();
        }
    });

    // 側欄底部的「收合選單」
    document.getElementById('sidebarCollapse')?.addEventListener('click', function () {
        if (isMobile()) {
            layout.classList.remove('is-open');
        } else {
            toggleCollapse();
        }
    });

    // 手機版：點側欄的關閉鈕或遮罩就收起來
    document.getElementById('sidebarClose')?.addEventListener('click', closeMobile);
    document.getElementById('adminBackdrop')?.addEventListener('click', closeMobile);

    function toggleCollapse() {
        const collapsed = layout.classList.toggle('is-collapsed');
        localStorage.setItem('venuego-sidebar', collapsed ? 'collapsed' : 'expanded');
    }

    function closeMobile() {
        layout.classList.remove('is-open');
    }

    // 全螢幕切換
    const fsBtn = document.getElementById('fullscreenBtn');
    fsBtn?.addEventListener('click', function () {
        if (document.fullscreenElement) {
            document.exitFullscreen();
        } else {
            document.documentElement.requestFullscreen();
        }
    });

    document.addEventListener('fullscreenchange', function () {
        const icon = fsBtn?.querySelector('i');
        if (!icon) return;
        icon.classList.toggle('bi-arrows-fullscreen', !document.fullscreenElement);
        icon.classList.toggle('bi-fullscreen-exit', !!document.fullscreenElement);
    });
})();
