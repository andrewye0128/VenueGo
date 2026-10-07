<!-- js -->
<script setup>
import IconUsers from "@/components/icons/IconUsers.vue";
import { RouterLink, RouterView, useRoute, useRouter } from "vue-router";
import { computed } from "vue";
import { useMemberStore } from "@/stores/member";
const memberStore = useMemberStore();
const route = useRoute();
const router = useRouter();
const sideMenuItem = [
  { label: "個人資料", icon: IconUsers, to: { name: "member-profile" } },
  { label: "我的預約", icon: IconUsers, to: { name: "not-found" } },
  { label: "訂單與付款", icon: IconUsers, to: { name: "not-found" } },
  { label: "我的票券", icon: IconUsers, to: { name: "tickets" } },
  { label: "我的評論", icon: IconUsers, to: { name: "not-found" } },
];
//測試資料
// const user = {
//   name: "王小明",
//   email: "member@example.com",
//   avatar: null,
// };
// const user = ref({
//   name: "",
//   email: "",
//   avatar: null,
// });
const user = computed(() => ({
  name: memberStore.member?.name ?? "",
  email: memberStore.member?.email ?? "",
  avatar: memberStore.member?.avatar ?? null,
}));

const logout = async () => {
  memberStore.logout();

  await router.push({ name: "login" });
};
// onMounted(async () => {
//   try {
//     const member = await getCurrentMember();

//     user.value.name = member.name;
//     user.value.email = member.email;
//   } catch (error) {
//     console.error("取得會員資料失敗：", error);
//   }
// });
</script>

<template>
  <div class="bg-gray-50">
    <div class="mx-auto max-w-6xl md:flex items-start px-4 py-6 gap-6">
      <!-- sideBar -->
      <aside class="w-56 shrink-0 border border-gray-200 bg-white">
        <div class="flex items-center border-b border-gray-200 p-4 gap-3">
          <div
            class="w-10 h-10 shrink-0 rounded-full bg-gray-700 text-white flex justify-center items-center text-xl font-bold"
          >
            {{ user.name.charAt(0) }}
          </div>
          <div class="min-w-0">
            <p class="text-sm font-semibold text-gray-900">{{ user.name }}</p>
            <p class="truncate text-xs text-gray-500">{{ user.email }}</p>
          </div>
        </div>

        <!-- nav -->
        <nav class="flex flex-col gap-1">
          <RouterLink
            v-for="item in sideMenuItem"
            :key="item.label"
            :to="item.to"
            class="flex items-center gap-3 px-4 py-3 rounded-lg text-sm transition-colors"
            :class="
              route.name === item.to.name
                ? 'bg-gray-900 text-white'
                : 'text-gray-600 hover:bg-gray-100'
            "
          >
            <component :is="item.icon" />
            {{ item.label }}
          </RouterLink>

          <button
            type="button"
            @click="logout"
            class="px-4 py-3 text-left text-sm text-red-600 hover:bg-gray-100"
          >
            登出
          </button>
        </nav>
      </aside>

      <!-- Main -->
      <main class="flex-1 min-w-0">
        <RouterView />
      </main>
    </div>
  </div>
</template>

<style scoped></style>
