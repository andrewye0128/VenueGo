<script setup>
import { reactive, ref } from "vue";
import { z } from "zod";
import { useRouter, RouterLink } from "vue-router";
import { registerMember } from "@/api/memberAuthApi";

const router = useRouter();

const schema = z
  .object({
    name: z.string().trim().min(1, "請輸入姓名").max(50, "姓名不可超過 50 個字"),
    email: z
      .string()
      .trim()
      .min(1, "請輸入 Email")
      .email("Email 格式不正確")
      .max(254, "Email 不可超過 254 個字"),
    password: z
      .string()
      .min(8, "密碼至少需要 8 個字元")
      .max(100, "密碼不可超過 100 個字元")
      .regex(/[a-z]/, "密碼需包含小寫英文字母")
      .regex(/[A-Z]/, "密碼需包含大寫英文字母")
      .regex(/[0-9]/, "密碼需包含數字"),
    confirmPassword: z.string().min(1, "請再次輸入密碼"),
    phone: z
      .string()
      .trim()
      .regex(/^09\d{8}$/, "請輸入 09 開頭的 10 碼手機號碼"),
    birth: z.string().min(1, "請選擇出生日期"),
    carrierNo: z
      .string()
      .trim()
      .refine(
        (value) => value === "" || /^\/[A-Za-z0-9.+-]{7}$/.test(value),
        "載具格式為「/」加上 7 碼英數字或符號",
      ),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "兩次輸入的密碼不一致",
    path: ["confirmPassword"],
  })
  .refine((data) => data.birth <= new Date().toLocaleDateString("en-CA"), {
    message: "生日不可晚於今天",
    path: ["birth"],
  })
  .refine((data) => data.birth >= "1900-01-01", {
    message: "生日不可早於 1900 年",
    path: ["birth"],
  });

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
const successMessage = ref("");
const fieldErrors = reactive({
  name: "",
  email: "",
  password: "",
  confirmPassword: "",
  phone: "",
  birth: "",
  carrierNo: "",
});

// async function onSubmit() {
//   errorMessage.value = "";
//   successMessage.value = "";
//   loading.value = true;

//   try {
//     await registerMember({
//       name: state.name.trim(),
//       email: state.email.trim(),
//       password: state.password,
//       confirmPassword: state.confirmPassword,
//       phone: state.phone.trim(),
//       birth: state.birth,
//       carrierNo: state.carrierNo.trim() || null,
//     });

//     successMessage.value = "註冊成功，正在前往登入頁面。";

//     await router.push({
//       name: "login",
//       query: { registered: "1" },
//     });
//   } catch (error) {
//     errorMessage.value = error.message || "註冊失敗，請稍後再試。";
//   } finally {
//     loading.value = false;
//   }
// }

async function onSubmit() {
  errorMessage.value = "";
  successMessage.value = "";

  // 清除前一次的欄位錯誤
  Object.keys(fieldErrors).forEach((key) => {
    fieldErrors[key] = "";
  });

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

    successMessage.value = "註冊成功，正在前往登入頁面。";

    await router.push({
      name: "login",
      query: { registered: "1" },
    });
  } catch (error) {
    const errors = error?.fieldErrors ?? {};

    // 將後端欄位錯誤對應到各個欄位
    Object.keys(fieldErrors).forEach((key) => {
      const message = errors[key];

      fieldErrors[key] = Array.isArray(message) ? (message[0] ?? "") : (message ?? "");
    });

    // 若沒有欄位錯誤，才顯示一般錯誤訊息
    const hasFieldErrors = Object.values(fieldErrors).some((message) => message !== "");

    if (!hasFieldErrors) {
      errorMessage.value = error?.message || "註冊失敗，請稍後再試。";
    }
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
        <p class="mt-2 text-sm leading-[22px] text-neutral-text-secondary">
          加入 VenueGo，開始預約場地
        </p>
      </div>

      <section class="rounded-md border border-neutral-border bg-neutral-surface p-6 md:p-8">
        <UForm :schema="schema" :state="state" class="space-y-4" @submit="onSubmit">
          <UFormField label="姓名" name="name" required :error="fieldErrors.name || undefined">
            <UInput v-model="state.name" placeholder="請輸入姓名" class="w-full" />
          </UFormField>

          <UFormField label="Email" name="email" required :error="fieldErrors.email || undefined">
            <UInput v-model="state.email" type="email" placeholder="請輸入 Email" class="w-full" />
          </UFormField>

          <UFormField
            label="密碼"
            name="password"
            required
            :error="fieldErrors.password || undefined"
          >
            <UInput
              v-model="state.password"
              :type="showPassword ? 'text' : 'password'"
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

          <UFormField
            label="確認密碼"
            name="confirmPassword"
            required
            :error="fieldErrors.confirmPassword || undefined"
          >
            <UInput
              v-model="state.confirmPassword"
              :type="showConfirmPassword ? 'text' : 'password'"
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

          <UFormField
            label="手機號碼"
            name="phone"
            required
            :error="fieldErrors.phone || undefined"
          >
            <UInput
              v-model="state.phone"
              type="tel"
              placeholder="例如：0912345678"
              class="w-full"
            />
          </UFormField>

          <UFormField
            label="出生日期"
            name="birth"
            required
            :error="fieldErrors.birth || undefined"
          >
            <UInput
              v-model="state.birth"
              type="date"
              :max="new Date().toLocaleDateString('en-CA')"
              min="1900-01-01"
              class="w-full"
            />
          </UFormField>

          <UFormField
            label="手機載具（選填）"
            name="carrierNo"
            :error="fieldErrors.carrierNo || undefined"
          >
            <UInput v-model="state.carrierNo" placeholder="例如：/ABC1234" class="w-full" />
          </UFormField>

          <div
            v-if="errorMessage"
            class="rounded-md border border-semantic-error px-3 py-2 text-sm text-semantic-error"
          >
            {{ errorMessage }}
          </div>

          <div
            v-if="successMessage"
            class="rounded-md border border-semantic-success px-3 py-2 text-sm"
          >
            {{ successMessage }}
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
