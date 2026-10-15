<script setup>
import { reactive, ref } from "vue";
import { z } from "zod";
import { useRouter, useRoute } from "vue-router";
import { useMemberStore } from "@/stores/member";

const router = useRouter();
const route = useRoute();
const memberStore = useMemberStore();
// 密碼顯示／隱藏
const showPassword = ref(false);

// 登入表單驗證規則
const schema = z.object({
  email: z.string().trim().min(1, "請輸入 Email").email("Email 格式不正確"),
  password: z.string().min(1, "請輸入密碼"),
});

// 表單資料
const state = reactive({
  email: "",
  password: "",
});

// 登入錯誤訊息
const loginError = ref("");

//會員註冊成功後，在登入頁看到「註冊成功，請登入
const successMessage = ref(
  route.query.registered === "1" ? "註冊成功，請使用您的 Email 與密碼登入。" : "",
);

// 登入
async function onSubmit() {
  loginError.value = "";

  try {
    await memberStore.login(state.email, state.password);
    //console.log(memberStore.member);
    await router.push({ name: "home" });
  } catch (error) {
    loginError.value = error.message;
  }
}
</script>

<template>
  <main class="min-h-[calc(100vh-160px)] px-4 py-10 md:py-16">
    <div class="mx-auto w-full max-w-md">
      <!-- 頁面標題 -->
      <div class="mb-8 text-center">
        <h1 class="text-2xl font-bold leading-8 text-neutral-text-primary">會員登入</h1>

        <p class="mt-2 text-sm leading-[22px] text-neutral-text-secondary">
          登入 VenueGo，開始預約場地
        </p>
      </div>

      <!-- 登入表單 Card -->
      <section class="rounded-md border border-neutral-border bg-neutral-surface p-6 md:p-8">
        <UForm :schema="schema" :state="state" class="space-y-5" @submit="onSubmit">
          <!-- Email -->
          <UFormField label="Email" name="email" required>
            <UInput v-model="state.email" type="email" placeholder="請輸入 Email" class="w-full" />
          </UFormField>

          <!-- 密碼 -->
          <UFormField label="密碼" name="password" required>
            <UInput
              v-model="state.password"
              :type="showPassword ? 'text' : 'password'"
              placeholder="請輸入密碼"
              autocomplete="current-password"
              class="w-full"
            >
              <!-- 眼睛按鈕 -->
              <template #trailing>
                <UButton
                  type="button"
                  color="neutral"
                  variant="ghost"
                  size="xs"
                  :icon="showPassword ? 'i-lucide-eye-off' : 'i-lucide-eye'"
                  :aria-label="showPassword ? '隱藏密碼' : '顯示密碼'"
                  :aria-pressed="showPassword"
                  class="cursor-pointer"
                  @click="showPassword = !showPassword"
                />
              </template>
            </UInput>
          </UFormField>

          <!-- 登入錯誤 -->
          <div
            v-if="loginError"
            class="rounded-md border border-semantic-error px-3 py-2 text-sm text-semantic-error"
          >
            {{ loginError }}
          </div>
          <!-- 註冊成功 -->
          <div
            v-if="successMessage"
            class="rounded-md border border-semantic-success px-3 py-2 text-sm"
          >
            {{ successMessage }}
          </div>
          <!-- 登入按鈕 -->
          <!--<UButton type="submit" color="primary" size="lg" block :loading="loading"> 登入 </UButton>-->
          <UButton type="submit" color="primary" size="lg" block :loading="memberStore.loading">
            登入
          </UButton>
          <!-- 其他功能 -->
          <div class="flex items-center justify-between pt-1 text-sm">
            <RouterLink to="/register" class="text-brand-primary hover:underline">
              註冊會員
            </RouterLink>

            <RouterLink to="/forgot-password" class="text-brand-primary hover:underline">
              忘記密碼？
            </RouterLink>
          </div>
        </UForm>
      </section>
    </div>
  </main>
</template>
