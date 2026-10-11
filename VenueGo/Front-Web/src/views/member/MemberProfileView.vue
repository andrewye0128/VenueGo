<script setup>
import { useMemberStore } from "@/stores/member";

const memberStore = useMemberStore();
</script>

<template>
  <section>
    <!-- 標題 -->
    <div class="mb-5 flex items-center justify-between">
      <div>
        <h1 class="text-xl font-bold text-gray-900">個人資料</h1>
        <p class="mt-1 text-sm text-gray-500">查看與管理您的會員資料</p>
      </div>

      <button
        type="button"
        class="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
      >
        編輯資料
      </button>
    </div>

    <!-- 個人資料卡片 -->
    <div class="border border-gray-200 bg-white">
      <div class="border-b border-gray-200 px-6 py-4">
        <h2 class="text-base font-semibold text-gray-900">個人資料</h2>
      </div>

      <div class="grid grid-cols-1 gap-x-10 gap-y-6 p-6 md:grid-cols-2">
        <!-- 姓名 -->
        <div>
          <p class="text-sm text-gray-500">姓名</p>

          <p class="mt-1 text-sm font-medium text-gray-900">
            {{ memberStore.member?.name || "-" }}
          </p>
        </div>

        <!-- Email -->
        <div>
          <p class="text-sm text-gray-500">Email</p>

          <p class="mt-1 text-sm font-medium text-gray-900">
            {{ memberStore.member?.email || "-" }}
          </p>
        </div>

        <!-- 手機 -->
        <div>
          <p class="text-sm text-gray-500">手機號碼</p>

          <p class="mt-1 text-sm font-medium text-gray-900">
            {{ memberStore.member?.phone || "-" }}
          </p>
        </div>
        <!-- 生日 -->
        <div>
          <p class="text-sm text-gray-500">生日</p>
          <p class="mt-1 text-sm font-medium text-gray-900">
            {{ memberStore.member?.birth || "-" }}
          </p>
        </div>
        <!-- 載具 -->
        <div>
          <p class="text-sm text-gray-500">載具</p>
          <p class="mt-1 text-sm font-medium text-gray-900">
            {{ memberStore.member?.carrierNo || "-" }}
          </p>
        </div>
      </div>
    </div>
  </section>
</template>
