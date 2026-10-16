<script setup>
import { reactive, ref } from "vue";
import { z } from "zod";
import { useRouter, RouterLink } from "vue-router";
import { registerMember } from "@/api/memberAuthApi";
import { ErrorCodes } from "@/constants/errorCodes";
import {
  birthRule,
  carrierNoRule,
  emailRule,
  mobileRule,
  nameRule,
  passwordRule,
} from "@/schemas/rules";

const router = useRouter();

// 各欄位規則共用 @/schemas/rules（改規則只要改那裡，所有表單一起更新）；
// 「兩次密碼要一致」只有這個表單需要，寫在這裡
const schema = z
  .object({
    name: nameRule,
    // 共用的 emailRule 只檢查必填與格式；後端 DTO 另外限制最多 254 字，這裡一併檢查
    email: emailRule.refine((value) => value.length <= 254, "Email 不可超過 254 個字"),
    password: passwordRule,
    confirmPassword: z.string().min(1, "請再次輸入密碼"),
    phone: mobileRule,
    birth: birthRule,
    carrierNo: carrierNoRule,
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "兩次輸入的密碼不一致",
    path: ["confirmPassword"],
  });

// 後端欄位錯誤要放回表單用（跟 ReviewWriteView 一樣的做法）
const formRef = ref(null);

const state = reactive({
  name: "",
  email: "",
  password: "",
  confirmPassword: "",
  phone: "",
  birth: "",
  carrierNo: "",
});

const showPassword = ref(false);
const showConfirmPassword = ref(false);
const loading = ref(false);
const errorMessage = ref("");

// 後端回來的錯誤：欄位錯誤（http.js 整理好的 error.fieldErrors）放回對應的欄位底下，
// 不屬於任何欄位的（key 是 "_" 或畫面上沒有的欄位）顯示在表單上方
function showServerError(error) {
  const known = new Set(Object.keys(state));
  const formErrors = [];
  let generalMessage = "";

  for (const [name, messages] of Object.entries(error.fieldErrors ?? {})) {
    const message = messages?.[0];
    if (!message) continue;

    if (known.has(name)) {
      formErrors.push({ name, message });
    } else if (!generalMessage) {
      generalMessage = message;
    }
  }

  // Email 已註冊：就算後端沒有附欄位錯誤，也要顯示在 Email 欄位底下
  if (
    error.errorCode === ErrorCodes.EmailAlreadyExists &&
    !formErrors.some((item) => item.name === "email")
  ) {
    formErrors.push({ name: "email", message: error.message });
  }

  formRef.value?.setErrors(formErrors);

  if (formErrors.length === 0 && !generalMessage) {
    generalMessage = error.message || "註冊失敗，請稍後再試。";
  }
  errorMessage.value = generalMessage;
}

async function onSubmit() {
  // 送出中不能再送一次：按鈕的 loading 擋得住滑鼠，擋不住在輸入框按 Enter
  if (loading.value) return;

  errorMessage.value = "";
  loading.value = true;

  try {
    await registerMember({
      name: state.name.trim(),
      email: state.email.trim(),
      password: state.password,
      confirmPassword: state.confirmPassword,
      phone: state.phone.trim(),
      birth: state.birth,
      carrierNo: state.carrierNo.trim() || null,
    });

    // 註冊成功：導到登入頁，登入頁會顯示「註冊成功」的提示（見 LoginView.vue）
    await router.push({ name: "login", query: { registered: "1" } });
  } catch (error) {
    showServerError(error);
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <main class="min-h-[calc(100vh-160px)] px-4 py-10 md:py-16">
    <div class="mx-auto w-full max-w-lg">
      <div class="mb-8 text-center">
        <h1 class="text-2xl font-bold leading-8 text-neutral-text-primary">註冊會員</h1>
        <p class="mt-2 text-sm leading-5.5 text-neutral-text-secondary">
          加入 VenueGo，開始預約場地
        </p>
      </div>

      <section class="rounded-md border border-neutral-border bg-neutral-surface p-6 md:p-8">
        <UForm ref="formRef" :schema="schema" :state="state" class="space-y-4" @submit="onSubmit">
          <UFormField label="姓名" name="name" required>
            <UInput
              v-model="state.name"
              autocomplete="name"
              placeholder="請輸入姓名"
              class="w-full"
            />
          </UFormField>

          <UFormField label="Email" name="email" required>
            <UInput
              v-model="state.email"
              type="email"
              autocomplete="email"
              placeholder="請輸入 Email"
              class="w-full"
            />
          </UFormField>

          <UFormField label="密碼" name="password" required>
            <UInput
              v-model="state.password"
              :type="showPassword ? 'text' : 'password'"
              autocomplete="new-password"
              placeholder="至少 8 個字元，包含大小寫英文及數字"
              class="w-full"
            >
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

          <UFormField label="確認密碼" name="confirmPassword" required>
            <UInput
              v-model="state.confirmPassword"
              :type="showConfirmPassword ? 'text' : 'password'"
              autocomplete="new-password"
              placeholder="請再次輸入密碼"
              class="w-full"
            >
              <template #trailing>
                <UButton
                  type="button"
                  color="neutral"
                  variant="ghost"
                  size="xs"
                  :icon="showConfirmPassword ? 'i-lucide-eye-off' : 'i-lucide-eye'"
                  :aria-label="showConfirmPassword ? '隱藏密碼' : '顯示密碼'"
                  :aria-pressed="showConfirmPassword"
                  class="cursor-pointer"
                  @click="showConfirmPassword = !showConfirmPassword"
                />
              </template>
            </UInput>
          </UFormField>

          <UFormField label="手機號碼" name="phone" required>
            <UInput
              v-model="state.phone"
              type="tel"
              autocomplete="tel"
              placeholder="例如：0912345678"
              class="w-full"
            />
          </UFormField>

          <UFormField label="出生日期" name="birth" required>
            <UInput
              v-model="state.birth"
              type="date"
              autocomplete="bday"
              :max="new Date().toLocaleDateString('en-CA')"
              min="1900-01-01"
              class="w-full"
            />
          </UFormField>

          <UFormField label="手機載具（選填）" name="carrierNo">
            <UInput v-model="state.carrierNo" placeholder="例如：/ABC1234" class="w-full" />
          </UFormField>

          <div
            v-if="errorMessage"
            class="rounded-md border border-semantic-error px-3 py-2 text-sm text-semantic-error"
          >
            {{ errorMessage }}
          </div>

          <UButton type="submit" color="primary" size="lg" block :loading="loading">
            建立會員帳號
          </UButton>

          <div class="pt-1 text-center text-sm">
            <span class="text-neutral-text-secondary">已經有帳號？</span>
            <RouterLink to="/login" class="ml-1 cursor-pointer text-brand-primary hover:underline">
              前往登入
            </RouterLink>
          </div>
        </UForm>
      </section>
    </div>
  </main>
</template>
