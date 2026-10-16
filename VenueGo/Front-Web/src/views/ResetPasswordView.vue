<script setup>
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { validateResetToken, resetMemberPassword } from "@/api/memberAuthApi";

const route = useRoute();
const router = useRouter();

const email = computed(() => String(route.query.email || "").trim());
const token = computed(() => String(route.query.token || "").trim());

const password = ref("");
const confirmPassword = ref("");

const loading = ref(false);
const validatingToken = ref(true);
const tokenValid = ref(false);

const errorMessage = ref("");
const successMessage = ref("");

onMounted(async () => {
  if (!email.value || !token.value) {
    errorMessage.value = "重設連結缺少 Email 或 Token，請重新申請。";
    validatingToken.value = false;
    return;
  }

  try {
    const result = await validateResetToken(email.value, token.value);

    if (!result?.success) {
      errorMessage.value = result?.message || "重設連結已失效，請重新申請。";
      return;
    }

    tokenValid.value = true;
  } catch (error) {
    errorMessage.value = error?.message || "無法驗證重設連結，請稍後再試。";
  } finally {
    validatingToken.value = false;
  }
});

async function submitResetPassword() {
  errorMessage.value = "";
  successMessage.value = "";

  if (!password.value || !confirmPassword.value) {
    errorMessage.value = "請輸入新密碼及確認密碼。";
    return;
  }

  if (password.value !== confirmPassword.value) {
    errorMessage.value = "兩次輸入的密碼不一致。";
    return;
  }

  loading.value = true;

  try {
    const result = await resetMemberPassword({
      email: email.value,
      token: token.value,
      password: password.value,
      confirmPassword: confirmPassword.value,
    });

    if (!result?.success) {
      errorMessage.value = result?.message || "重設密碼失敗，請重新確認資料。";
      return;
    }

    successMessage.value = result.message || "密碼已重設成功。";
    password.value = "";
    confirmPassword.value = "";
    tokenValid.value = false;
  } catch (error) {
    errorMessage.value = error?.message || "目前無法重設密碼，請稍後再試。";
  } finally {
    loading.value = false;
  }
}

function goToLogin() {
  router.push({ name: "login" });
}
</script>

<template>
  <main class="mx-auto w-full max-w-md px-4 py-12">
    <section class="rounded-2xl border border-gray-200 bg-white p-6 shadow-sm">
      <h1 class="text-2xl font-bold text-gray-900">重設密碼</h1>

      <p class="mt-2 text-sm text-gray-600">請設定新密碼，並再次輸入以確認。</p>

      <p v-if="email" class="mt-3 break-all text-sm text-gray-600">帳號：{{ email }}</p>

      <div v-if="validatingToken" class="mt-6 text-sm text-gray-600">正在驗證重設連結……</div>

      <form v-else-if="tokenValid" class="mt-6 space-y-4" @submit.prevent="submitResetPassword">
        <div>
          <label for="password" class="mb-1 block text-sm font-medium text-gray-700">
            新密碼
          </label>
          <input
            id="password"
            v-model="password"
            type="password"
            autocomplete="new-password"
            required
            minlength="8"
            placeholder="請輸入新密碼"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-brand-primary focus:ring-2 focus:ring-brand-primary/20"
          />
        </div>

        <div>
          <label for="confirmPassword" class="mb-1 block text-sm font-medium text-gray-700">
            確認新密碼
          </label>
          <input
            id="confirmPassword"
            v-model="confirmPassword"
            type="password"
            autocomplete="new-password"
            required
            minlength="8"
            placeholder="請再次輸入新密碼"
            class="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-brand-primary focus:ring-2 focus:ring-brand-primary/20"
          />
        </div>

        <p v-if="errorMessage" role="alert" class="text-sm text-red-600">
          {{ errorMessage }}
        </p>

        <p v-if="successMessage" role="status" class="text-sm text-green-700">
          {{ successMessage }}
        </p>

        <button
          type="submit"
          :disabled="loading"
          class="w-full rounded-lg bg-brand-primary px-4 py-2.5 font-semibold text-white transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {{ loading ? "處理中..." : "確認重設密碼" }}
        </button>

        <button
          v-if="successMessage"
          type="button"
          class="w-full rounded-lg border border-gray-300 px-4 py-2.5 font-semibold text-gray-700 hover:bg-gray-50"
          @click="goToLogin"
        >
          返回登入
        </button>
      </form>

      <div v-else class="mt-6 space-y-4">
        <p v-if="errorMessage" role="alert" class="text-sm text-red-600">
          {{ errorMessage }}
        </p>

        <p class="text-sm text-gray-600">請返回忘記密碼頁面，重新申請重設連結。</p>

        <button
          type="button"
          class="w-full rounded-lg bg-brand-primary px-4 py-2.5 font-semibold text-white hover:opacity-90"
          @click="router.push('/forgot-password')"
        >
          重新申請重設連結
        </button>
      </div>

      <div class="mt-5 text-center text-sm">
        <RouterLink to="/login" class="text-brand-primary hover:underline"> 返回登入 </RouterLink>
      </div>
    </section>
  </main>
</template>
