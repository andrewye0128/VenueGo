// 長按連續觸發（昱，10/23）：按下去先觸發一次，按住不放就每隔一段時間再觸發一次，像「慢慢連點」。
// 用在換頁按鈕、輪播箭頭這類「按一次前進一步」的地方。說明見 Front-Web/docs/useRepeatPress_Claude.md
//
// 用法：
//   const press = useRepeatPress({
//     step: (dir, isRepeat) => { ... },   // 每觸發一次就呼叫；isRepeat：是不是按住之後自動觸發的
//     finish: (dir, repeated) => { ... }, // 放開時呼叫一次；repeated：這次有沒有自動前進過
//   });
//   press.start("next");  // 滑鼠、手指按下，或鍵盤按下
//   press.stop();         // 放開
//   press.activeDir.value // 正在按的方向（沒有在按是 null），畫面用來讓按鈕「亮起來」
//   press.repeating.value // 是否已經認定為長按（時間軸見下方）
//
// ── 為什麼自己算時間，不用鍵盤的自動重複 ──
//  按住鍵盤時，瀏覽器會照作業系統的速度連續送出按鍵事件（一秒幾十次，而且每台電腦不一樣）。
//  所以鍵盤的重複事件要由呼叫端忽略（event.repeat），統一由這裡每 interval 毫秒觸發一次。
//
// ── 什麼時候會自動放開 ──
//  切到別的視窗、別的分頁時，可能收不到「放開」的事件（滑鼠在別的視窗放開、鍵盤 keyup 送給別的視窗），
//  所以視窗失去焦點、分頁被隱藏、元件被移除時，都當成放開，不會一直自己翻下去。
import { onBeforeUnmount, onMounted, ref } from "vue";

/** 按住多久才算「長按」：過了這個時間 repeating 變成 true（評論專區這時會出現快轉遮罩），但還不前進 */
export const HOLD_DELAY_MS = 300;
/** 長按之後每隔多久前進一步 */
export const REPEAT_INTERVAL_MS = 500;

// 時間軸（預設值）：
//   0 秒      按下 → 前進一步（跟點一下一樣）
//   0.3 秒    認定是長按 → repeating = true，畫面可以開始顯示「正在快轉」，停在剛才那一步上
//   0.8 秒    自動前進第一步，之後每 0.5 秒一步
// 10/23：原本是按下 0.5 秒就「認定長按＋同時前進」，快轉遮罩一出現就已經跳過一頁
//        （從第 1 頁按住，遮罩直接從第 3 頁開始）。改成先認定、再前進，每一頁在遮罩上都停一樣久。
export function useRepeatPress({
  step,
  finish,
  holdDelay = HOLD_DELAY_MS,
  interval = REPEAT_INTERVAL_MS,
}) {
  const activeDir = ref(null);
  const repeating = ref(false);
  let timer = null;
  let autoSteps = 0; // 這次按住之後自動前進了幾步

  function tick() {
    autoSteps++;
    step(activeDir.value, true);
    timer = setTimeout(tick, interval);
  }

  function hold() {
    repeating.value = true;
    timer = setTimeout(tick, interval);
  }

  function start(dir) {
    if (activeDir.value !== null) stop(); // 換方向（例如按著 → 又按 ←）：先把上一個結束
    activeDir.value = dir;
    repeating.value = false;
    autoSteps = 0;
    step(dir, false);
    timer = setTimeout(hold, holdDelay);
  }

  function stop() {
    if (activeDir.value === null) return;
    clearTimeout(timer);
    timer = null;
    const dir = activeDir.value;
    const repeated = autoSteps > 0; // 只「認定長按」但還沒自動前進的，不算
    activeDir.value = null;
    repeating.value = false;
    finish?.(dir, repeated);
  }

  function onVisibilityChange() {
    if (document.visibilityState === "hidden") stop();
  }

  onMounted(() => {
    window.addEventListener("blur", stop);
    document.addEventListener("visibilitychange", onVisibilityChange);
  });
  onBeforeUnmount(() => {
    stop();
    window.removeEventListener("blur", stop);
    document.removeEventListener("visibilitychange", onVisibilityChange);
  });

  return { start, stop, activeDir, repeating };
}

/**
 * 給「按鈕」用的事件處理：按下開始、放開（或手指滑走、被系統取消）結束。用法：<button v-bind="pressHandlers(press, 'next')">
 * 用 pointer capture：按下之後就算游標移出按鈕，放開的事件還是會送回這顆按鈕，不會卡在「一直按著」。
 * 只處理滑鼠左鍵、手指、觸控筆；右鍵、中鍵不算。
 *
 * 鍵盤（Tab 到按鈕再按 Enter／空白鍵）不會觸發 pointer 事件，只會觸發 click，所以 click 當成「點一下」。
 * 但滑鼠、手指放開之後瀏覽器也會補送一次 click，那一次要忽略，不然會多走一步。
 * 不用 event.detail 判斷是不是鍵盤：手機上點一下送來的 click，detail 不一定是 1（實測模擬器是 0）。
 */
export function pressHandlers(press, dir) {
  let fromPointer = false; // 接下來的 click 是不是滑鼠、手指放開時補送的
  return {
    onPointerdown(e) {
      if (e.pointerType === "mouse" && e.button !== 0) return;
      e.preventDefault(); // 不要選取文字、不要觸發手機的長按選單
      e.currentTarget.setPointerCapture?.(e.pointerId);
      fromPointer = true;
      press.start(dir);
    },
    onPointerup: () => press.stop(),
    onPointercancel: () => press.stop(),
    onLostpointercapture: () => press.stop(),
    onContextmenu: (e) => e.preventDefault(), // 手機長按會跳出系統選單
    onKeydown(e) {
      if (e.key === "Enter" || e.key === " ") fromPointer = false; // 接下來的 click 是鍵盤送的
    },
    onClick() {
      if (fromPointer) {
        fromPointer = false;
        return;
      }
      press.start(dir);
      press.stop();
    },
  };
}
