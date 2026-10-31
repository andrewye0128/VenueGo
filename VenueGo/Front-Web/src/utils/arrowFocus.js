// 用方向鍵在一組按鈕之間移動焦點（昱，10/31）
//
// 用法：把 keydown 掛在包住那組按鈕的元素上
//   <div @keydown="moveFocusByArrows">…按鈕…</div>
//   <div @keydown="moveFocusByArrows($event, { vertical: true })">…</div>   // 上下鍵也算（手機上按鈕直排時）
//
// 用在：確認視窗（components/ConfirmModal.vue）、評論查看頁底部的兩顆按鈕、已評論的表態按鈕。
// 焦點不在這組按鈕上、或已經在最左／最右邊時不動；停用的按鈕跳過。
const HORIZONTAL = Object.freeze({ ArrowLeft: -1, ArrowRight: 1 });
const VERTICAL = Object.freeze({ ArrowUp: -1, ArrowDown: 1 });

export function moveFocusByArrows(event, { vertical = false } = {}) {
  const step = HORIZONTAL[event.key] ?? (vertical ? VERTICAL[event.key] : undefined);
  if (step === undefined) return;
  const buttons = [...event.currentTarget.querySelectorAll("a[href], button:not([disabled])")];
  const index = buttons.indexOf(document.activeElement);
  if (index === -1) return;
  const next = buttons[index + step];
  if (!next) return;
  event.preventDefault();
  next.focus();
}
