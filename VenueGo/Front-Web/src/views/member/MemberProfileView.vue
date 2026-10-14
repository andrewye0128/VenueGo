<script setup>
import { ref } from "vue";
import { useMemberStore } from "@/stores/member";
import { updateMemberProfile } from "@/api/memberProfileApi";

const memberStore = useMemberStore();

const isEditing = ref(false);
const saving = ref(false);

const successMessage = ref("");
const errorMessage = ref(""); // 用於 API 失敗或未歸類的全域錯誤

// 各欄位的獨立錯誤訊息物件
const fieldErrors = ref({
  name: "",
  phone: "",
  carrierNo: "",
  currentPassword: "",
  newPassword: "",
  confirmNewPassword: "",
});

const showCurrentPassword = ref(false);
const showNewPassword = ref(false);
const showConfirmPassword = ref(false);

const form = ref({
  name: "",
  email: "",
  phone: "",
  birth: "",
  carrierNo: "",
  currentPassword: "",
  newPassword: "",
  confirmNewPassword: "",
});

// 清空所有錯誤
function clearErrors() {
  errorMessage.value = "";
  successMessage.value = "";
  Object.keys(fieldErrors.value).forEach((key) => {
    fieldErrors.value[key] = "";
  });
}

// 開始編輯
function startEditing() {
  clearErrors();

  form.value = {
    name: memberStore.member?.name ?? "",
    email: memberStore.member?.email ?? "",
    phone: memberStore.member?.phone ?? "",
    birth: memberStore.member?.birth ?? "",
    carrierNo: memberStore.member?.carrierNo ?? "",
    currentPassword: "",
    newPassword: "",
    confirmNewPassword: "",
  };

  showCurrentPassword.value = false;
  showNewPassword.value = false;
  showConfirmPassword.value = false;

  isEditing.value = true;
}

// 取消編輯
function cancelEditing() {
  isEditing.value = false;
  clearErrors();
}

// 儲存個人資料
async function saveProfile() {
  clearErrors();

  const name = form.value.name.trim();
  const phone = form.value.phone.trim();
  const carrierNo = form.value.carrierNo.trim().toUpperCase();

  const currentPassword = form.value.currentPassword;
  const newPassword = form.value.newPassword;
  const confirmNewPassword = form.value.confirmNewPassword;

  let isValid = true;

  // 姓名驗證
  if (!name) {
    fieldErrors.value.name = "請輸入姓名";
    isValid = false;
  } else if (name.length > 50) {
    fieldErrors.value.name = "姓名長度不能超過 50 個字";
    isValid = false;
  }

  // 台灣手機號碼：09 開頭，共 10 碼
  if (!phone) {
    fieldErrors.value.phone = "請輸入手機號碼";
    isValid = false;
  } else if (!/^09\d{8}$/.test(phone)) {
    fieldErrors.value.phone = "手機號碼格式不正確，請輸入 09 開頭的 10 碼手機號碼";
    isValid = false;
  }

  // 載具可以留空，有填寫才驗證
  if (carrierNo && !/^\/[A-Z0-9.+-]{7}$/.test(carrierNo)) {
    fieldErrors.value.carrierNo = "載具格式不正確，請輸入斜線開頭及 7 碼英數字或 +、-、. 字元";
    isValid = false;
  }

  // 任一密碼欄位有填寫，就視為要修改密碼
  const wantsToChangePassword =
    currentPassword !== "" || newPassword !== "" || confirmNewPassword !== "";

  if (wantsToChangePassword) {
    if (!currentPassword) {
      fieldErrors.value.currentPassword = "請輸入目前密碼";
      isValid = false;
    }

    if (!newPassword) {
      fieldErrors.value.newPassword = "請輸入新密碼";
      isValid = false;
    } else {
      if (newPassword.length < 8 || newPassword.length > 100) {
        fieldErrors.value.newPassword = "新密碼長度需介於 8 至 100 個字元";
        isValid = false;
      } else if (!/(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/.test(newPassword)) {
        fieldErrors.value.newPassword = "新密碼必須包含大寫英文字母、小寫英文字母與數字";
        isValid = false;
      }
    }

    if (!confirmNewPassword) {
      fieldErrors.value.confirmNewPassword = "請再次輸入新密碼";
      isValid = false;
    } else if (newPassword !== confirmNewPassword) {
      fieldErrors.value.confirmNewPassword = "兩次輸入的新密碼不一致";
      isValid = false;
    }
  }

  if (!isValid) return;

  saving.value = true;

  try {
    await updateMemberProfile({
      name,
      phone,
      carrierNo: carrierNo || null,
      currentPassword: currentPassword || null,
      newPassword: newPassword || null,
    });

    await memberStore.fetchCurrentMember();

    isEditing.value = false;
    successMessage.value = "個人資料更新成功";
  } catch (error) {
    // console.error("更新個人資料失敗：", error);

    // const data = error?.response?.data;
    // const errors = data?.errors;

    // const firstValidationMessage = errors
    //   ? Object.values(errors)
    //       .flat()
    //       .find((message) => typeof message === "string" && message)
    //   : null;

    // errorMessage.value =
    //   firstValidationMessage ||
    //   data?.message ||
    //   data?.title ||
    //   error?.message ||
    //   "更新個人資料失敗，請稍後再試。";

    const data = error?.response?.data;
    const errors = data?.data?.errors ?? data?.errors;

    // 後端欄位名稱 → 前端 fieldErrors 欄位名稱
    const fieldNameMap = {
      name: "name",
      phone: "phone",
      carrierno: "carrierNo",
      currentpassword: "currentPassword",
      newpassword: "newPassword",
      confirmnewpassword: "confirmNewPassword",
    };

    let firstUnknownMessage = "";

    if (errors && typeof errors === "object") {
      for (const [key, value] of Object.entries(errors)) {
        const messages = Array.isArray(value) ? value : [value];

        const message = messages.find((item) => typeof item === "string" && item);

        if (!message) continue;

        // 支援 Name、name，以及 request.Name 這類欄位名稱
        const propertyName = key.split(".").pop().toLowerCase();
        const fieldName = fieldNameMap[propertyName];

        if (fieldName) {
          fieldErrors.value[fieldName] = message;
        } else if (!firstUnknownMessage) {
          firstUnknownMessage = message;
        }
      }
    }

    errorMessage.value =
      firstUnknownMessage ||
      (!errors || Object.keys(errors).length === 0
        ? data?.message || data?.title || error?.message || "更新個人資料失敗，請稍後再試。"
        : "");
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <section class="mx-auto w-full max-w-5xl space-y-6 pb-8">
    <!-- 頁面標題 -->
    <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 class="text-2xl font-bold text-neutral-text-primary">個人資料</h1>
        <p class="mt-2 text-sm text-neutral-text-secondary">管理您的會員資訊與帳號安全設定</p>
      </div>

      <div class="flex items-center gap-2">
        <template v-if="isEditing">
          <UButton
            type="button"
            color="neutral"
            variant="outline"
            :disabled="saving"
            class="cursor-pointer"
            @click="cancelEditing"
          >
            取消
          </UButton>

          <UButton
            type="submit"
            form="member-profile-form"
            color="primary"
            :loading="saving"
            class="cursor-pointer"
          >
            儲存變更
          </UButton>
        </template>

        <UButton v-else type="button" color="primary" class="cursor-pointer" @click="startEditing">
          <UIcon name="i-lucide-pencil" class="mr-1 size-4" />
          編輯資料
        </UButton>
      </div>
    </div>

    <!-- 成功訊息 -->
    <UAlert
      v-if="successMessage"
      color="success"
      variant="subtle"
      title="操作成功"
      :description="successMessage"
    />

    <!-- 錯誤訊息 -->
    <UAlert
      v-if="errorMessage"
      color="error"
      variant="subtle"
      title="操作失敗"
      :description="errorMessage"
    />

    <UForm id="member-profile-form" :state="form" class="space-y-6" @submit="saveProfile">
      <!-- 基本資料 -->
      <UCard>
        <template #header>
          <div>
            <h2 class="font-semibold text-neutral-text-primary">基本資料</h2>
            <p class="mt-1 text-sm text-neutral-text-secondary">您的姓名與聯絡資訊</p>
          </div>
        </template>

        <div class="grid grid-cols-1 gap-x-8 gap-y-6 sm:grid-cols-2">
          <!-- 姓名 -->
          <UFormField label="姓名" name="name" required :error="fieldErrors.name || undefined">
            <UInput
              v-if="isEditing"
              v-model="form.name"
              class="w-full"
              placeholder="請輸入姓名"
              maxlength="50"
              autocomplete="name"
            />
            <p v-else class="py-2 text-sm font-medium text-neutral-text-primary">
              {{ memberStore.member?.name || "尚未填寫" }}
            </p>
          </UFormField>

          <!-- Email -->
          <UFormField label="Email">
            <div class="flex min-h-10 items-center gap-2">
              <p class="break-all text-sm text-neutral-text-primary">
                {{ memberStore.member?.email || "尚未填寫" }}
              </p>
            </div>
            <p v-if="isEditing" class="mt-1 text-xs text-neutral-text-secondary">
              Email 為登入帳號，目前無法自行修改。
            </p>
          </UFormField>

          <!-- 手機 -->
          <UFormField
            label="手機號碼"
            name="phone"
            required
            :error="fieldErrors.phone || undefined"
          >
            <UInput
              v-if="isEditing"
              v-model="form.phone"
              class="w-full"
              type="tel"
              inputmode="numeric"
              maxlength="10"
              autocomplete="tel"
              placeholder="例如 0912345678"
            />
            <p v-else class="py-2 text-sm font-medium text-neutral-text-primary">
              {{ memberStore.member?.phone || "尚未填寫" }}
            </p>
            <p
              v-if="isEditing && !fieldErrors.phone"
              class="mt-1 text-xs text-neutral-text-secondary"
            >
              請輸入 09 開頭的 10 碼台灣手機號碼。
            </p>
          </UFormField>

          <!-- 生日 -->
          <UFormField label="生日">
            <div class="flex min-h-10 items-center gap-2">
              <p class="text-sm text-neutral-text-primary">
                {{ memberStore.member?.birth || "尚未填寫" }}
              </p>
            </div>
          </UFormField>

          <!-- 載具 -->
          <UFormField
            label="電子發票載具"
            name="carrierNo"
            class="sm:col-span-2"
            :error="fieldErrors.carrierNo || undefined"
          >
            <UInput
              v-if="isEditing"
              v-model="form.carrierNo"
              class="w-full sm:max-w-md"
              placeholder="例如 /AB12345"
              maxlength="8"
              autocomplete="off"
              autocapitalize="characters"
            />
            <p
              v-if="isEditing && !fieldErrors.carrierNo"
              class="mt-1 text-xs text-neutral-text-secondary"
            >
              請輸入斜線開頭，後接 7 碼英數字或 +、-、. 字元。
            </p>
            <p v-else-if="!isEditing" class="py-2 text-sm font-medium text-neutral-text-primary">
              {{ memberStore.member?.carrierNo || "尚未設定載具" }}
            </p>
          </UFormField>
        </div>
      </UCard>

      <!-- 修改密碼 -->
      <UCard v-if="isEditing">
        <template #header>
          <div>
            <h2 class="font-semibold text-neutral-text-primary">修改密碼</h2>
            <p class="mt-1 text-sm text-neutral-text-secondary">
              若不需要修改密碼，請將以下欄位全部留空。
            </p>
          </div>
        </template>

        <div class="space-y-6">
          <!-- 舊密碼 -->
          <UFormField
            label="舊密碼"
            name="currentPassword"
            :error="fieldErrors.currentPassword || undefined"
          >
            <UInput
              v-model="form.currentPassword"
              class="w-full"
              :type="showCurrentPassword ? 'text' : 'password'"
              autocomplete="current-password"
              placeholder="請輸入原密碼"
            >
              <template #trailing>
                <UButton
                  type="button"
                  color="neutral"
                  variant="ghost"
                  size="xs"
                  :icon="showCurrentPassword ? 'i-lucide-eye-off' : 'i-lucide-eye'"
                  :aria-label="showCurrentPassword ? '隱藏密碼' : '顯示密碼'"
                  class="cursor-pointer"
                  @click="showCurrentPassword = !showCurrentPassword"
                />
              </template>
            </UInput>
          </UFormField>

          <div class="grid grid-cols-1 gap-6 md:grid-cols-2">
            <!-- 新密碼 -->
            <UFormField
              label="新密碼"
              name="newPassword"
              :error="fieldErrors.newPassword || undefined"
            >
              <UInput
                v-model="form.newPassword"
                class="w-full"
                :type="showNewPassword ? 'text' : 'password'"
                autocomplete="new-password"
                placeholder="請輸入新密碼"
              >
                <template #trailing>
                  <UButton
                    type="button"
                    color="neutral"
                    variant="ghost"
                    size="xs"
                    :icon="showNewPassword ? 'i-lucide-eye-off' : 'i-lucide-eye'"
                    :aria-label="showNewPassword ? '隱藏密碼' : '顯示密碼'"
                    class="cursor-pointer"
                    @click="showNewPassword = !showNewPassword"
                  />
                </template>
              </UInput>
              <p v-if="!fieldErrors.newPassword" class="mt-1 text-xs text-neutral-text-secondary">
                至少 8 個字元，包含大寫、小寫英文字母與數字。
              </p>
            </UFormField>

            <!-- 確認新密碼 -->
            <UFormField
              label="確認新密碼"
              name="confirmNewPassword"
              :error="fieldErrors.confirmNewPassword || undefined"
            >
              <UInput
                v-model="form.confirmNewPassword"
                class="w-full"
                :type="showConfirmPassword ? 'text' : 'password'"
                autocomplete="new-password"
                placeholder="請再次輸入新密碼"
              >
                <template #trailing>
                  <UButton
                    type="button"
                    color="neutral"
                    variant="ghost"
                    size="xs"
                    :icon="showConfirmPassword ? 'i-lucide-eye-off' : 'i-lucide-eye'"
                    :aria-label="showConfirmPassword ? '隱藏密碼' : '顯示密碼'"
                    class="cursor-pointer"
                    @click="showConfirmPassword = !showConfirmPassword"
                  />
                </template>
              </UInput>
            </UFormField>
          </div>
        </div>
      </UCard>

      <!-- 底部操作 -->
      <div
        v-if="isEditing"
        class="flex flex-col-reverse gap-3 sm:flex-row sm:items-center sm:justify-between"
      >
        <p class="text-xs text-neutral-text-secondary">
          修改密碼需要輸入舊密碼；不修改時，密碼欄位全部留空即可。
        </p>

        <div class="flex justify-end gap-2">
          <UButton
            type="button"
            color="neutral"
            variant="outline"
            :disabled="saving"
            class="cursor-pointer"
            @click="cancelEditing"
          >
            取消
          </UButton>

          <UButton type="submit" color="primary" :loading="saving" class="cursor-pointer">
            儲存變更
          </UButton>
        </div>
      </div>
    </UForm>
  </section>
</template>
