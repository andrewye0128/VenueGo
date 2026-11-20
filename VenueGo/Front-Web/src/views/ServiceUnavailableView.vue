<script setup>
// 暫時無法確認登入狀態時顯示（路由守衛帶過來的，網址帶 ?redirect=原本要去的頁面）
// 後端沒回應、資料庫斷線時，登入狀態還保留著，按「重試」就回到原本要去的頁面
import { ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { safeRedirect } from "@/utils/safeRedirect";
import { delay } from "@/utils/delay";

// 重試時按鈕至少轉多久（毫秒）
// 資料庫剛連不上時，後端接下來幾秒會直接回「失敗」（不再真的去連），
// 不到 0.1 秒就有結果，轉圈圈一閃就沒了，客人會以為按了沒反應
const MIN_RETRY_MS = 800;

const route = useRoute();
const router = useRouter();

// 重試中：按鈕顯示轉圈圈，而且按不下去，避免連按
const retrying = ref(false);
// 重試了幾次都還是連不上：每按一次數字都會變，客人知道確實有按到；0 表示還沒重試過
const failedRetries = ref(0);

// 回到原本要去的頁面；路由守衛會再確認一次登入狀態
// 還是連不上的話，守衛又會帶回這一頁
// replace：這一頁不留在「上一頁」紀錄裡，按上一頁不會又回到錯誤頁
async function retry() {
  retrying.value = true;
  try {
    // 換頁和「至少轉 MIN_RETRY_MS」同時進行，兩個都結束才往下（後端很慢時不會多等）
    await Promise.all([
      router.replace(safeRedirect(route.query.redirect) ?? { name: "home" }),
      delay(MIN_RETRY_MS),
    ]);
  } finally {
    retrying.value = false;
    // 還停在這一頁：守衛又把客人帶回來了（還是連不上）
    if (route.name === "service-unavailable") {
      failedRetries.value++;
    }
  }
}
</script>

<template>
  <!-- 排版和顏色跟 404 頁（NotFoundView.vue）一致；UButton 由 Nuxt UI 自動匯入 -->
  <main class="mx-auto flex max-w-7xl flex-col items-center px-4 py-20 text-center md:px-6">
    <h1 class="text-2xl font-bold text-neutral-text-primary">暫時無法確認登入狀態</h1>
    <p class="mt-2 text-sm text-neutral-text-secondary">
      伺服器暫時沒有回應，你的登入狀態還保留著，請稍後再試。
    </p>
    <!-- aria-live：螢幕閱讀器也會唸出「還是無法連線」 -->
    <p aria-live="polite" class="mt-2 text-sm text-neutral-text-secondary">
      <template v-if="failedRetries > 0">
        還是無法連線（已重試 {{ failedRetries }} 次），請稍後再試。
      </template>
    </p>
    <div class="mt-8 flex gap-3">
      <UButton color="action" :loading="retrying" @click="retry">重試</UButton>
      <UButton color="primary" variant="outline" to="/" :disabled="retrying">回到首頁</UButton>
    </div>
  </main>
</template>
