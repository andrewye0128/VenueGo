import { ref, computed } from "vue";
import { defineStore } from "pinia";
import { memberLogin, getCurrentMember } from "@/api/memberAuthApi";
import { isSessionInvalidError } from "@/utils/authErrors";

// 確認登入狀態（ensureSession）的三種結果
export const SessionStatus = Object.freeze({
  LoggedIn: "loggedIn", // 登入有效，會員資料也拿到了
  LoggedOut: "loggedOut", // 沒登入，或登入已經失效（已經自動登出）
  Unavailable: "unavailable", // 後端暫時不能回答；登入狀態保留，等一下再試
});

export const useMemberStore = defineStore("member", () => {
  // 目前登入會員
  const member = ref(null);

  // 載入狀態
  const loading = ref(false);

  // 是否已登入
  const isLoggedIn = computed(() => !!member.value);

  // 正在向後端確認身分的那一次請求；沒有在確認時是 null
  // 同時有好幾個地方要確認時（例如網站剛打開），大家共用同一次，不會重複問後端
  let pendingCheck = null;

  // 是否正在向後端確認身分（components/NavigationLoading.vue 用來顯示「正在確認登入狀態」）
  const checkingSession = ref(false);

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

  // 確認目前的登入狀態，回傳 SessionStatus 的其中一種
  // 路由守衛、App.vue 都用這個，「什麼情況要登出」的規則只寫在這裡
  async function ensureSession() {
    // 沒有 token：一定沒登入（也可能是在別的分頁登出了），把殘留的會員資料清掉
    if (!localStorage.getItem("token")) {
      logout();
      return SessionStatus.LoggedOut;
    }

    // 已經確認過、會員資料還在：不用再問後端
    if (member.value) {
      return SessionStatus.LoggedIn;
    }

    // 還沒確認過：向後端確認；已經有人在確認的話，等同一次的結果
    if (!pendingCheck) {
      checkingSession.value = true;
      pendingCheck = checkWithServer().finally(() => {
        pendingCheck = null;
        checkingSession.value = false;
      });
    }
    return pendingCheck;
  }

  // 向後端要會員資料，依結果決定登入狀態（只給 ensureSession 用）
  async function checkWithServer() {
    try {
      await fetchCurrentMember();
      return SessionStatus.LoggedIn;
    } catch (error) {
      // 登入失效（401）：登出，請客人重新登入
      if (isSessionInvalidError(error)) {
        logout();
        return SessionStatus.LoggedOut;
      }
      // 其他原因（後端沒開、資料庫斷線…）：登入沒有問題，不登出，等一下再試
      return SessionStatus.Unavailable;
    }
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
    checkingSession,
    login,
    fetchCurrentMember,
    ensureSession,
    logout,
  };
});
