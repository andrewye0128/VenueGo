# VenueGo 會員登入狀態使用說明

目前專案已經使用 **Pinia** 統一管理會員登入狀態。

組員如果需要取得目前登入會員的資料，**不用自己呼叫 `/member/auth/me`，也不要自己另外存一份會員資料**。

直接使用：

```js
import { useMemberStore } from "@/stores/member";
```

---

## 一、取得會員 Store

在 `<script setup>` 中：

```js
import { useMemberStore } from "@/stores/member";

const memberStore = useMemberStore();
```

之後就可以使用：

```js
memberStore.member;
memberStore.isLoggedIn;
memberStore.loading;
```

---

## 二、取得目前登入會員

目前登入會員會放在：

```js
memberStore.member;
```

例如：

```js
const memberStore = useMemberStore();

console.log(memberStore.member);
```

會員資料可以直接使用：

```js
memberStore.member.name;
memberStore.member.email;
memberStore.member.avatar;
```

在 Template 中：

```vue
<p>{{ memberStore.member?.name }}</p>
<p>{{ memberStore.member?.email }}</p>
```

建議使用 `?.`，避免會員資料尚未載入時發生錯誤。

---

## 三、判斷會員是否登入

可以使用：

```js
memberStore.isLoggedIn;
```

例如：

```vue
<div v-if="memberStore.isLoggedIn">
  已登入
</div>

<div v-else>
  尚未登入
</div>
```

不需要自己寫：

```js
localStorage.getItem("token");
```

來判斷登入狀態。

---

## 四、如果需要重新取得會員資料

如果頁面需要重新從後端取得最新會員資料，可以使用：

```js
await memberStore.fetchCurrentMember();
```

例如：

```js
async function refreshMember() {
  await memberStore.fetchCurrentMember();
}
```

這個方法會呼叫後端：

```text
GET /api/member/auth/me
```

並自動更新：

```js
memberStore.member;
```

所以組員**不用自己 import `getCurrentMember()`**。

---

## 五、會員登入

一般頁面不需要自己處理登入。

登入頁面統一使用：

```js
await memberStore.login(email, password);
```

例如：

```js
async function login() {
  try {
    await memberStore.login(state.email, state.password);

    // 登入成功
    await router.push({ name: "member" });
  } catch (error) {
    console.log(error.message);
  }
}
```

`memberStore.login()` 會自動處理：

```text
登入 API
 ↓
取得 JWT Token
 ↓
儲存 Token
 ↓
取得目前會員資料 /me
 ↓
更新 memberStore.member
```

所以組員不用自己處理這些步驟。

---

## 六、登出

如果需要登出：

```js
memberStore.logout();
```

例如：

```js
async function logout() {
  memberStore.logout();

  await router.push({ name: "login" });
}
```

`logout()` 會自動：

1. 清除 Token
2. 清除目前會員資料

也就是：

```js
localStorage.removeItem("token");
memberStore.member = null;
```

這些不用組員自己寫。

---

## 七、完整範例

假設組員正在做「會員資料」頁面：

```vue
<script setup>
import { useMemberStore } from "@/stores/member";

const memberStore = useMemberStore();
</script>

<template>
  <div>
    <h1>會員資料</h1>

    <p>姓名：{{ memberStore.member?.name }}</p>

    <p>Email：{{ memberStore.member?.email }}</p>

    <p>
      登入狀態：
      {{ memberStore.isLoggedIn ? "已登入" : "未登入" }}
    </p>
  </div>
</template>
```

這樣就可以直接使用目前登入會員。

---

## 八、組員不要這樣做

### ❌ 不要自己呼叫 `/me`

不要在每個頁面都寫：

```js
import { getCurrentMember } from "@/api/memberAuthApi";

const member = await getCurrentMember();
```

因為目前已經由：

```text
memberStore
```

統一管理。

應該改成：

```js
const memberStore = useMemberStore();

memberStore.member;
```

---

### ❌ 不要自己另外建立會員狀態

例如不要每個頁面都：

```js
const member = ref(null);
```

然後自己取得會員資料。

否則不同頁面可能會各自存一份會員資料，之後容易不同步。

統一使用：

```js
memberStore.member;
```

---

### ❌ 不要自己判斷 Token

不要在頁面裡到處寫：

```js
const token = localStorage.getItem("token");
```

登入權限已經由 Router Guard 處理。

`/member/*` 頁面如果沒有登入，會自動導向：

```text
/login
```

---

# 九、最常用的三個東西

組員其實記住下面三個就可以：

### 取得會員

```js
memberStore.member;
```

### 判斷是否登入

```js
memberStore.isLoggedIn;
```

### 重新取得會員資料

```js
await memberStore.fetchCurrentMember();
```

---

# 十、簡單記法

之後只要是在會員相關頁面：

```js
import { useMemberStore } from "@/stores/member";

const memberStore = useMemberStore();
```

然後：

```js
memberStore.member;
```

就是目前登入會員。

```js
memberStore.isLoggedIn;
```

就是登入狀態。

```js
await memberStore.fetchCurrentMember();
```

就是重新取得會員資料。

```js
memberStore.logout();
```

就是登出。

**不要在各個頁面重新寫登入、Token、`/me` API。**

統一透過 `memberStore` 使用即可。
