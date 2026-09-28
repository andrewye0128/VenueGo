<script setup>
// 前端驗證範例：預約表單（給組員參考）
// UForm、UFormField、UInput、UTextarea、UCheckbox、UButton 由 Nuxt UI 自動匯入
import { reactive, ref } from "vue";
import { z } from "zod";
import SportTypeSelect from "@/components/SportTypeSelect.vue";
import { nameRule, emailRule, mobileRule, mustAgreeRule } from "@/schemas/rules";

// 這張表單的驗證規則：共用規則 + 這張表單特有的規則
const schema = z.object({
  name: nameRule,
  email: emailRule,
  phone: mobileRule,
  sportType: z.string().min(1, "請選擇運動類型"),
  people: z
    .number({ error: "請輸入人數" })
    .int("人數必須是整數")
    .min(1, "人數至少 1 人")
    .max(20, "人數最多 20 人"),
  note: z.string().max(200, "備註最多 200 字"),
  agree: mustAgreeRule("請先閱讀並同意使用條款"),
});

// 表單目前的值；欄位名稱要跟 schema、UFormField 的 name 一致
const state = reactive({
  name: "",
  email: "",
  phone: "",
  sportType: "",
  people: undefined,
  note: "",
  agree: false,
});

const submitted = ref(null);

// 全部欄位都通過驗證才會執行；event.data 是驗證後的資料（例如姓名前後空白已去掉）
async function onSubmit(event) {
  // 模擬呼叫後端 API 等待 1.5 秒；這段期間送出按鈕會自動顯示轉圈圖示、不能重複按
  await new Promise((resolve) => setTimeout(resolve, 1500));
  submitted.value = event.data;
}
</script>

<template>
  <main class="mx-auto max-w-7xl px-4 py-10 md:px-6">
    <h1 class="text-2xl font-bold text-neutral-text-primary">前端驗證範例：預約表單</h1>
    <p class="mt-2 text-sm text-neutral-text-secondary">
      離開欄位時才檢查；直接按「送出預約」會一次檢查全部欄位。規則寫在 src/schemas/rules.js 與本頁的
      schema。
    </p>

    <div class="mt-8 grid gap-8 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
      <section class="rounded border border-neutral-border bg-neutral-surface p-6">
        <UForm :schema="schema" :state="state" class="space-y-5" @submit="onSubmit">
          <UFormField label="姓名" name="name" required>
            <UInput v-model="state.name" placeholder="請輸入姓名" class="w-full" />
          </UFormField>

          <UFormField label="Email" name="email" required>
            <UInput
              v-model="state.email"
              type="email"
              placeholder="example@mail.com"
              class="w-full"
            />
          </UFormField>

          <UFormField label="手機" name="phone" required>
            <UInput v-model="state.phone" type="tel" placeholder="0912345678" class="w-full" />
          </UFormField>

          <SportTypeSelect v-model="state.sportType" name="sportType" required />

          <UFormField label="人數" name="people" required>
            <UInput
              v-model.number="state.people"
              type="number"
              placeholder="1 ~ 20"
              class="w-full"
            />
          </UFormField>

          <UFormField label="備註" name="note" hint="選填，最多 200 字">
            <UTextarea v-model="state.note" :rows="3" class="w-full" />
          </UFormField>

          <UFormField name="agree">
            <UCheckbox v-model="state.agree" label="我已閱讀並同意使用條款" />
          </UFormField>

          <UButton type="submit" color="action" size="lg" block>送出預約</UButton>
        </UForm>
      </section>

      <section class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">送出的資料</h2>
        <pre
          v-if="submitted"
          class="overflow-x-auto rounded bg-brand-background p-4 text-xs text-neutral-text-primary"
        ><code>{{ JSON.stringify(submitted, null, 2) }}</code></pre>
        <p v-else class="text-sm text-neutral-text-secondary">
          全部欄位通過驗證並送出後，這裡會顯示送出的資料。
        </p>
      </section>
    </div>
  </main>
</template>
