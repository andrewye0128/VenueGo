import { ref, computed } from "vue";
import { defineStore } from "pinia";
import { memberLogin, getCurrentMember } from "@/api/memberAuthApi";

export const useMemberStore = defineStore("member", () => {
  // 目前登入會員
  const member = ref(null);

  // 載入狀態
  const loading = ref(false);

  // 是否已登入
  const isLoggedIn = computed(() => !!member.value);

  // 會員登入
  async function login(email, password) {
    loading.value = true;

    try {
      const result = await memberLogin(email, password);

      localStorage.setItem("token", result.token);

      await fetchCurrentMember();

      return result;
    } finally {
      loading.value = false;
    }
  }

  // 取得目前登入會員
  async function fetchCurrentMember() {
    const currentMember = await getCurrentMember();

    member.value = currentMember;

    return currentMember;
  }

  // 登出
  function logout() {
    localStorage.removeItem("token");
    member.value = null;
  }

  return {
    member,
    loading,
    isLoggedIn,
    login,
    fetchCurrentMember,
    logout,
  };
});
