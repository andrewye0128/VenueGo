# VenueGo 前台「現在時間」使用說明（useSiteClock）

前台要顯示「現在」、算倒數、判斷「是不是已經過了」的時候，**統一用 `useSiteClock`**，不要自己 `new Date()`。

```js
import { useSiteClock } from "@/composables/useSiteClock";
```

---

## 一、為什麼不要自己 `new Date()`

後端有一個開發用的**時光機**，可以讓整個網站「以為現在是某一天」，方便測試「預約前一天」「評論快過期」這些情況。

- 後端：所有用 `ITimeService.Now` 的地方都會跟著時光機走。
- 前台：`new Date()` 是瀏覽器自己的時鐘，**不會**跟著時光機走。

所以如果你的頁面自己 `new Date()`，時光機一開，畫面上的時間就會和後端算出來的結果對不起來。例如後端說「已過期」，畫面卻還顯示「還有 3 天」。

用 `useSiteClock`：

- 開發時（`npm run dev`、後端有開）→ 跟著時光機走
- 正式版本 → 就是瀏覽器的時間，跟 `new Date()` 一樣

---

## 二、基本用法

```vue
<script setup>
import { computed } from "vue";
import { useSiteClock } from "@/composables/useSiteClock";

const { now } = useSiteClock();

// now 是 ref(Date)，每秒自動更新
const today = computed(() => now.value.toLocaleDateString("zh-TW"));
</script>

<template>
  <p>今天是 {{ today }}</p>
</template>
```

`now.value` 就是一個普通的 `Date`，`getFullYear()`、`getHours()` 這些都能用。

---

## 三、算倒數、判斷有沒有過期

```js
const { now } = useSiteClock();

// 後端給的期限（字串），例如 "2026-10-30T16:45:00"
const expiredAt = new Date(review.expiredAt);

const isExpired = computed(() => now.value >= expiredAt);
const daysLeft = computed(() => Math.ceil((expiredAt - now.value) / 86_400_000));
```

`now` 每秒更新，所以 `isExpired`、`daysLeft` 會自己跟著變，不用自己寫 `setInterval`。

---

## 四、時光機相關（一般頁面用不到）

| 名稱 | 說明 |
|---|---|
| `available` | 有沒有時光機可以用（開發環境、後端有開） |
| `traveling` | 時光機是不是正在旅行中 |
| `offsetText` | 「快 22 天 17 小時」這種文字；沒在旅行是空字串 |
| `realNow` | 真實時間（回到現在之後會變成的時間） |
| `openPanel()` | 用小視窗打開時光機面板 |
| `resetToNow()` | 回到真實時間（失敗會丟出 Error） |

TopBar 的時間顯示就是用這幾個做的，可以參考 `src/layouts/TopBar.vue`。

---

## 五、不要這樣做

### ❌ 自己 `new Date()`

```js
const now = new Date();   // 不會跟著時光機走
```

改成：

```js
const { now } = useSiteClock();
// 用 now.value
```

### ❌ 自己寫每秒更新的計時器

```js
const now = ref(new Date());
setInterval(() => (now.value = new Date()), 1000);   // 不用了
```

`useSiteClock` 已經每秒更新，所有元件共用同一個計時器；元件全部移除時，計時器也會自動停掉。

### ❌ 自己呼叫 `/api/dev/time`

時光機的 API 只給 `useSiteClock` 用。一般頁面只要拿 `now` 就好。

---

## 六、簡單記法

```js
import { useSiteClock } from "@/composables/useSiteClock";

const { now } = useSiteClock();
```

`now.value` 就是「現在」。
