// 線上付款流程：向後端開一張綠界付款單，再整頁跳到綠界刷卡頁
// 「訂單與付款」頁、預約完成頁共用，付款的細節只寫在這裡
//
// 用法（在頁面的 setup 裡呼叫）：
//   const { isPaying, isBusy, pay } = useOrderPayment({ onStateChanged: () => load({ silent: true }) });
//   <UButton :loading="isPaying(order)" :disabled="isBusy" :loading-auto="false" @click="pay(order)">付款</UButton>
//
// onStateChanged：訂單的付款狀態可能變了（例如另一個分頁付過了、從綠界按上一頁回來），頁面要重新讀取
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useToast } from "@nuxt/ui/composables";
import { startPayment } from "@/api/orderApi";
import { ErrorCodes } from "@/constants/errorCodes";
import { useMemberStore } from "@/stores/member";
import { submitToEcpay } from "@/utils/ecpayRedirect";

// 這些錯誤代表「畫面上的狀態已經過時」：例如另一個分頁付過了、剛好超過期限。提示後要重新讀取
const STALE_STATE_ERRORS = [
  ErrorCodes.AlreadyPaid,
  ErrorCodes.NotPayable,
  ErrorCodes.PastStartDeadline,
  ErrorCodes.NotOnlinePayment,
  ErrorCodes.Conflict,
  ErrorCodes.NotFound,
];

/**
 * @param {object} [options]
 * @param {() => void} [options.onStateChanged] 付款狀態可能變了，頁面要重新讀取
 */
export function useOrderPayment({ onStateChanged = () => {} } = {}) {
  const route = useRoute();
  const router = useRouter();
  const memberStore = useMemberStore();
  const toast = useToast();

  // 正在付款的訂單 Id：這筆的按鈕轉圈，其他付款按鈕暫時不能按；null 表示沒有在付款
  // 不用全站的自動轉圈（loadingAuto）：它只轉到 pay 結束，但送出表單後瀏覽器還要一下子才換頁，
  // 這段時間按鈕會恢復成可以按，客人可能再按一次。所以成功後不清掉，讓它一直轉到離開這一頁
  const payingOrderId = ref(null);
  const isBusy = computed(() => payingOrderId.value !== null);
  const isPaying = (order) => payingOrderId.value === order.orderId;

  /**
   * 開始付款：向後端開付款單 → 整頁跳到綠界刷卡頁
   * @param {{ orderId: number }} order 訂單（列表或明細的資料都可以）
   */
  async function pay(order) {
    if (isBusy.value) return;
    payingOrderId.value = order.orderId;
    try {
      const checkout = await startPayment(order.orderId);
      submitToEcpay(checkout);
    } catch (err) {
      payingOrderId.value = null;
      handleError(err);
    }
  }

  // 訊息一律用後端的 message：規則只寫在後端，之後改文字也只改一個地方
  function handleError(err) {
    if (err.errorCode === ErrorCodes.NotLoggedIn) {
      // 登入失效：登出並回登入頁，登入後回到這一頁
      memberStore.logout();
      router.push({ name: "login", query: { redirect: route.fullPath } });
      return;
    }

    if (err.errorCode === ErrorCodes.InProgress) {
      // 上一筆付款還在確認中：重新讀取後會顯示「付款處理中」，付款按鈕也會消失
      toast.add({ title: err.message, color: "warning", icon: "i-mdi-alert-outline" });
      onStateChanged();
      return;
    }

    if (STALE_STATE_ERRORS.includes(err.errorCode)) {
      toast.add({ title: err.message, color: "error", icon: "i-mdi-alert-circle-outline" });
      onStateChanged();
      return;
    }

    // 線上付款暫時不能用（NotConfigured、TradeNoUnavailable）、連不到伺服器、付款網址異常：
    // 訂單本身沒有變，不用重新讀取
    toast.add({
      title: err.message || "無法開始付款，請稍後再試",
      color: "error",
      icon: "i-mdi-alert-circle-outline",
    });
  }

  // 從綠界按「上一頁」回來：瀏覽器可能直接拿出離開前的畫面（bfcache），付款按鈕會停在轉圈
  // event.persisted 為 true 就是這種情況：停止轉圈，並重新讀取（這時付款已經是「付款處理中」）
  function onPageShow(event) {
    if (!event.persisted) return;
    payingOrderId.value = null;
    onStateChanged();
  }
  onMounted(() => window.addEventListener("pageshow", onPageShow));
  onBeforeUnmount(() => window.removeEventListener("pageshow", onPageShow));

  return { isBusy, isPaying, pay };
}
