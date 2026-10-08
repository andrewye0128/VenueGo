<script setup>
import { ref } from "vue";
import { useMemberStore } from "@/stores/member";
import { updateMemberProfile } from "@/api/memberProfileApi";

const memberStore = useMemberStore();

const isEditing = ref(false);
const saving = ref(false);

const errorMessage = ref("");
const successMessage = ref("");

const form = ref({
  name: "",
  email: "",
  phone: "",
  birth: "",
  carrierNo: "",

  // 修改密碼
  currentPassword: "",
  newPassword: "",
  confirmNewPassword: "",
});

function startEditing() {
  errorMessage.value = "";
  successMessage.value = "";

  form.value = {
    name: memberStore.member?.name ?? "",
    email: memberStore.member?.email ?? "",
    phone: memberStore.member?.phone ?? "",
    birth: memberStore.member?.birth ?? "",
    carrierNo: memberStore.member?.carrierNo ?? "",

    // 修改密碼時一律清空
    currentPassword: "",
    newPassword: "",
    confirmNewPassword: "",
  };

  isEditing.value = true;
}

function cancelEditing() {
  isEditing.value = false;
  errorMessage.value = "";
  successMessage.value = "";
}

async function saveProfile() {
  errorMessage.value = "";
  successMessage.value = "";

  const phone = form.value.phone.trim();
  const carrierNo = form.value.carrierNo.trim();
  // 前端基本驗證
  // 姓名
  if (!form.value.name.trim()) {
    errorMessage.value = "請輸入姓名";
    return;
  }

  // 手機號碼
  if (!phone) {
    errorMessage.value = "請輸入手機號碼";
    return;
  }

  if (!/^09\d{8}$/.test(phone)) {
    errorMessage.value = "手機號碼格式不正確，請輸入 09 開頭的 10 碼手機號碼";
    return;
  }

  // 載具：有填才驗證，沒填可以儲存
  if (carrierNo && !/^\/[A-Z0-9.+-]{7}$/.test(carrierNo)) {
    errorMessage.value = "載具格式不正確，例如 /AB12.+-";
    return;
  }
  saving.value = true;

  try {
    await updateMemberProfile({
      name: form.value.name.trim(),

      // Email 目前不可編輯，
      // 但後端 UserProfileViewModel.Email 為 Required，
      // 所以仍然把原本的 Email 傳回去。
      email: form.value.email.trim(),

      phone: form.value.phone.trim(),

      // 生日目前不可編輯，而且後端更新 API 沒有處理 Birth，
      // 因此不送 birth。

      carrierNo: form.value.carrierNo.trim(),
    });

    // 更新成功後重新取得會員資料
    await memberStore.fetchCurrentMember();

    isEditing.value = false;
    successMessage.value = "個人資料更新成功";
  } catch (error) {
    console.error("更新個人資料失敗：", error);

    errorMessage.value = error?.response?.data?.message || error?.message || "更新個人資料失敗";
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <section>
    <!-- 標題 -->
    <div class="mb-5 flex items-center justify-between">
      <div>
        <h1 class="text-xl font-bold text-gray-900">個人資料</h1>

        <p class="mt-1 text-sm text-gray-500">查看與管理您的會員資料</p>
      </div>

      <!-- 非編輯狀態 -->
      <button
        v-if="!isEditing"
        type="button"
        class="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
        @click="startEditing"
      >
        編輯資料
      </button>

      <!-- 編輯狀態 -->
      <div v-else class="flex gap-2">
        <!-- 取消 -->
        <button
          type="button"
          class="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-50"
          :disabled="saving"
          @click="cancelEditing"
        >
          取消
        </button>

        <!-- 儲存 -->
        <button
          type="button"
          class="rounded-md bg-brand-primary px-4 py-2 text-sm font-medium text-white hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
          :disabled="saving"
          @click="saveProfile"
        >
          {{ saving ? "儲存中..." : "儲存" }}
        </button>
      </div>
    </div>

    <!-- 成功訊息 -->
    <div
      v-if="successMessage"
      class="mb-4 rounded-md border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700"
    >
      {{ successMessage }}
    </div>

    <!-- 錯誤訊息 -->
    <div
      v-if="errorMessage"
      class="mb-4 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
    >
      {{ errorMessage }}
    </div>

    <!-- 個人資料卡片 -->
    <div class="border border-gray-200 bg-white">
      <!-- 卡片標題 -->
      <div class="border-b border-gray-200 px-6 py-4">
        <h2 class="text-base font-semibold text-gray-900">個人資料</h2>
      </div>

      <!-- 資料內容 -->
      <div class="grid grid-cols-1 gap-x-10 gap-y-6 p-6 md:grid-cols-2">
        <!-- 姓名 -->
        <div>
          <p class="text-sm text-gray-500">姓名</p>

          <!-- 編輯模式 -->
          <input
            v-if="isEditing"
            v-model="form.name"
            type="text"
            class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
          />

          <!-- 顯示模式 -->
          <p v-else class="mt-1 text-sm font-medium text-gray-900">
            {{ memberStore.member?.name || "-" }}
          </p>
        </div>

        <!-- Email -->
        <div>
          <p class="text-sm text-gray-500">Email</p>

          <!-- Email 不可編輯 -->
          <p class="mt-1 text-sm font-medium text-gray-900">
            {{ memberStore.member?.email || "-" }}
          </p>
          <p
            v-if="isEditing"
            class="mt-2 inline-block rounded bg-yellow-50 px-2 py-1 text-xs text-yellow-700"
          >
            Email 為登入帳號，如需修改請聯絡管理員
          </p>
        </div>

        <!-- 手機號碼 -->
        <div>
          <p class="text-sm text-gray-500">手機號碼</p>

          <!-- 編輯模式 -->
          <input
            v-if="isEditing"
            v-model="form.phone"
            type="text"
            class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
          />

          <!-- 顯示模式 -->
          <p v-else class="mt-1 text-sm font-medium text-gray-900">
            {{ memberStore.member?.phone || "-" }}
          </p>
        </div>

        <!-- 生日 -->
        <div>
          <p class="text-sm text-gray-500">生日</p>

          <!-- 生日不可編輯 -->
          <p class="mt-1 text-sm font-medium text-gray-900">
            {{ memberStore.member?.birth || "-" }}
          </p>
        </div>

        <!-- 載具 -->
        <div>
          <p class="text-sm text-gray-500">載具</p>

          <!-- 編輯模式 -->
          <input
            v-if="isEditing"
            v-model="form.carrierNo"
            type="text"
            placeholder="例如 /AB12.+-"
            class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
          />

          <!-- 顯示模式 -->
          <p v-else class="mt-1 text-sm font-medium text-gray-900">
            {{ memberStore.member?.carrierNo || "-" }}
          </p>
        </div>
      </div>

      <div v-if="isEditing" class="mt-6 border border-gray-200 bg-white">
        <div class="border-b border-gray-200 px-6 py-4">
          <h2 class="text-base font-semibold text-gray-900">修改密碼</h2>
          <p class="mt-1 text-sm text-gray-500">如果不需要修改密碼，請留空</p>
        </div>

        <div class="grid grid-cols-1 gap-x-10 gap-y-6 p-6">
          <div>
            <label class="text-sm text-gray-500">目前密碼</label>
            <input
              v-if="isEditing"
              v-model="form.currentPassword"
              type="password"
              class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
            />
          </div>

          <div>
            <label class="text-sm text-gray-500">新密碼</label>
            <input
              v-if="isEditing"
              v-model="form.newPassword"
              type="password"
              class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
            />
          </div>

          <div>
            <label class="text-sm text-gray-500">確認新密碼</label>
            <input
              v-if="isEditing"
              v-model="form.confirmNewPassword"
              type="password"
              class="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
            />
          </div>
        </div>
      </div>
    </div>
  </section>
</template>
