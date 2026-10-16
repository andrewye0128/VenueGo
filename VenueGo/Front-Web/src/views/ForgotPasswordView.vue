<script setup>
import { ref } from "vue";
import { forgotPassword } from "@/api/memberAuthApi";

const email = ref("");
const loading = ref(false);
const errorMessage = ref("");
const successMessage = ref("");
const resetToken = ref("");

async function submitForgotPassword() {
  errorMessage.value = "";
  successMessage.value = "";
  resetToken.value = "";

  const normalizedEmail = email.value.trim();

  if (!normalizedEmail) {
    errorMessage.value = "請輸入 Email。";
    return;
  }

  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalizedEmail)) {
    errorMessage.value = "Email 格式不正確。";
    return;
  }

  loading.value = true;

  try {
    const result = await forgotPassword(normalizedEmail);

    if (!result?.success) {
      errorMessage.value = result?.message || "申請失敗，請稍後再試。";
      return;
    }

    successMessage.value =
      result.message || "如果此 Email 已註冊且帳號有效，您將可以繼續重設密碼流程。";

    // 後端只會在 Development 環境回傳 Token。
    resetToken.value = result.data?.resetToken || "";
  } catch (error) {
    errorMessage.value =
      error?.response?.data?.message || error?.message || "目前無法送出申請，請稍後再試。";
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <main class="mx-auto w-full max-w-md px-4 py-12">
    <section class="rounded-2xl border border-gray-200 bg-white p-6 shadow-sm">
      <h1 class="text-2xl font-bold text-gray-900">忘記密碼</h1>

      <p class="mt-2 text-sm text-gray-600">請輸入註冊時使用的 Email，以繼續重設密碼流程。</p>

      <form class="mt-6 space-y-4" @submit.prevent="submitForgotPassword">
        <div>
          <label for="email" class="mb-1 block text-sm font-medium text-gray-700"> Email </label>

          <input
            id="email"
            v-model="email"
            type="email"
            autocomplete="email"
            required
            placeholder="請輸入註冊 Email"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-brand-primary focus:ring-2 focus:ring-brand-primary/20"
          />
        </div>

        <p v-if="errorMessage" role="alert" class="text-sm text-red-600">
          {{ errorMessage }}
        </p>

        <p v-if="successMessage" role="status" class="text-sm text-green-700">
          {{ successMessage }}
        </p>

        <div v-if="resetToken" class="rounded-lg border border-amber-300 bg-amber-50 p-3">
          <p class="text-sm font-medium text-amber-900">開發測試用重設連結</p>

          <RouterLink
            :to="{
              path: '/reset-password',
              query: { email: email.trim(), token: resetToken },
            }"
            class="mt-2 inline-block break-all text-sm text-brand-primary underline"
          >
            前往重設密碼
          </RouterLink>

          <p class="mt-2 text-xs text-amber-800">
            此連結僅供開發環境測試使用，Token 有效期限為 30 分鐘。
          </p>
        </div>

        <button
          type="submit"
          :disabled="loading"
          class="w-full rounded-lg bg-brand-primary px-4 py-2.5 font-semibold text-white transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {{ loading ? "送出中..." : "送出重設申請" }}
        </button>
      </form>

      <div class="mt-5 text-center text-sm">
        <RouterLink to="/login" class="text-brand-primary hover:underline"> 返回登入 </RouterLink>
      </div>
    </section>
  </main>
</template>
