# 長按連續觸發：`useRepeatPress`

`src/composables/useRepeatPress.js`（昱，10/23）

按一下前進一步；**按住不放**就連續前進，像「慢慢連點」。滑鼠、手指、鍵盤都適用。

| 時間 | 發生什麼 |
|---|---|
| 0 秒 | 按下 → 前進一步（跟點一下一樣） |
| 0.3 秒 | 認定是長按：`repeating` 變成 true（評論專區這時出現快轉遮罩），還不前進 |
| 0.8 秒起 | 每 0.5 秒前進一步 |

先「認定長按」、隔 0.5 秒才自動前進：快轉遮罩一出現就是下一頁，每一頁在遮罩上停的時間都一樣（10/23 調整）。兩個時間都可以用 `holdDelay`、`interval` 改。
評論專區的換頁、首頁「顧客評價」輪播的箭頭在用。首頁輪播（10/24）改成 `holdDelay: 200`、`interval: 100`：沒有遮罩，0.8 秒才動會以為沒反應；而且要的是「拉霸」的感覺，轉的時候看不清楚沒關係。
`interval` 不要再小：輪播一張的動畫大約就是 0.1 秒，再快的話會「欠」好幾張，放開之後還要轉一陣子才停。其他「按一次前進一步」的按鈕（例如場地介紹的輪播箭頭）也可以直接拿去用。

## 用法

```vue
<script setup>
import { useRepeatPress, pressHandlers } from "@/composables/useRepeatPress";

const press = useRepeatPress({
  // 每前進一步就呼叫一次。isRepeat：是不是「按住之後」自動觸發的（第一下是 false）
  step(dir, isRepeat) {
    if (dir === "next") index.value++;
    else if (dir === "prev") index.value--;
  },
  // 放開時呼叫一次（可省略）。repeated：這次有沒有自動前進過
  finish(dir, repeated) {},
});
</script>

<template>
  <!-- 滑鼠、手指、Tab＋Enter 都會處理好；按住時 data-pressed 可以拿來做「亮起來」的樣式 -->
  <button
    type="button"
    v-bind="pressHandlers(press, 'next')"
    :data-pressed="press.activeDir.value === 'next' ? 'true' : undefined"
  >
    下一個
  </button>
</template>
```

鍵盤方向鍵要自己接 `keydown`／`keyup`：`keydown` 呼叫 `press.start(dir)`、`keyup` 呼叫 `press.stop()`。
**`event.repeat` 為 true 的 keydown 要忽略**——按住鍵盤時瀏覽器會照作業系統的速度狂送事件（一秒幾十次），
節奏統一由 `useRepeatPress` 控制。範例見 `components/review/ReviewPager.vue` 的 `onKeydown`。

## 注意

| 情況 | 處理方式 |
|---|---|
| 到第一頁／最後一頁 | 在 `step` 裡不動就好。**按鈕不要用 `disabled`**，改用 `aria-disabled`：按鈕在按住的途中被停用，瀏覽器就不會再送「放開」給它，會停不下來 |
| 手機長按跳出系統選單 | `pressHandlers` 已經擋掉（`contextmenu`），CSS 再加 `-webkit-touch-callout: none` 更保險 |
| 放開時瀏覽器補送的 click | `pressHandlers` 已經忽略，不會多走一步 |
| 切到別的視窗、分頁 | 自動當成放開（可能收不到放開的事件） |

## 為什麼不是「按住越久越快」

刻意固定每 0.5 秒一步：使用者要能看清楚「現在到哪裡」，加速之後很容易衝過頭。
評論專區還會在長按期間顯示每一頁第一則的日期、星等，讓人知道大概翻到排序的哪個位置。
