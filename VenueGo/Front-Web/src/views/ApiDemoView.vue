<script setup>
// 前後端 API 連線測試頁（給組員參考）
// UButton 由 Nuxt UI 自動匯入
import { ref } from "vue";
import http from "@/api/http";
import { getPing } from "@/api/pingApi";

// 目前結果：{ ok: true, data } 或 { ok: false, message }
const result = ref(null);

async function testPing() {
  result.value = null;
  try {
    const data = await getPing();
    result.value = { ok: true, data };
  } catch (error) {
    // http.js 已經把錯誤轉成中文訊息，這裡只要顯示 error.message
    result.value = { ok: false, message: error.message };
  }
}

// 示範錯誤處理：呼叫一支不存在的 API，會得到 404
async function testNotFound() {
  result.value = null;
  try {
    await http.get("/not-exist");
  } catch (error) {
    result.value = { ok: false, message: error.message };
  }
}

const usageExample = `import { getPing } from "@/api/pingApi";

try {
  const data = await getPing();   // { reply: "pong", time: "..." }
} catch (error) {
  console.log(error.message);     // 已轉成中文，例如「無法連線到伺服器，請稍後再試」
}`;
</script>

<template>
  <main class="mx-auto max-w-7xl px-4 py-10 md:px-6">
    <h1 class="text-2xl font-bold text-neutral-text-primary">前後端 API 連線測試</h1>
    <p class="mt-2 text-sm text-neutral-text-secondary">
      呼叫後端的 GET /api/ping。請先用 Visual Studio 的 https
      設定檔啟動後端（https://localhost:7078）。
    </p>

    <div class="mt-8 grid gap-8 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
      <section class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">測試</h2>
        <div class="flex flex-wrap gap-3">
          <!-- @click 綁定 async 函式時，按鈕會自動轉圈直到完成（全站預設 loadingAuto） -->
          <UButton color="action" @click="testPing">測試連線</UButton>
          <UButton color="primary" variant="outline" @click="testNotFound">
            測試錯誤（呼叫不存在的 API）
          </UButton>
        </div>

        <div
          v-if="result?.ok"
          class="mt-6 rounded border border-semantic-success p-4 text-sm text-semantic-success"
        >
          <p class="font-semibold">連線成功</p>
          <p class="mt-1 tabular-nums">
            後端回傳：{{ result.data.reply }}（伺服器時間 {{ result.data.time }}）
          </p>
        </div>
        <div
          v-else-if="result"
          class="mt-6 rounded border border-semantic-error p-4 text-sm text-semantic-error"
        >
          <p class="font-semibold">連線失敗</p>
          <p class="mt-1">{{ result.message }}</p>
        </div>
      </section>

      <section class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">組員的使用方式</h2>
        <pre
          class="overflow-x-auto rounded bg-brand-background p-4 text-xs text-neutral-text-primary"
        ><code>{{ usageExample }}</code></pre>
        <p class="mt-3 text-xs text-neutral-text-secondary">
          新的 API 請放在 src/api/，並從 src/api/http.js 發出請求。
        </p>
      </section>
    </div>
  </main>
</template>
