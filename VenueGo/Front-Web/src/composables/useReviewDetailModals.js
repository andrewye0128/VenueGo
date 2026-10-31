// 評論頁打開「預約明細」「訂單明細」視窗（昱，10/31）
//
// 用法：
//   const detail = useReviewDetailModals();
//   detail.open(item.kind, item.orderId);   // 現場評論 → 預約明細；預約評論 → 訂單明細
//   detail.label(item.kind)                 // 按鈕、aria-label 用的文字：「查看預約詳情」「查看訂單詳情」
//   detail.busy                             // 正在查資料（ref），按鈕可以拿來顯示轉圈、避免連按；視窗打開之後就解除
//
// 用在：我的評論（待評論、已評論）、撰寫頁、查看頁。
//
// 兩個視窗都是 F3 的元件，這裡只負責「找到資料、打開」，不改它們：
//   現場評論 → components/reservations/ReservationDetailModal.vue（要傳「我的預約」列表裡的那一筆）
//   預約評論 → components/orders/OrderDetailModal.vue（傳訂單，它自己會讀明細）
//
// ── 現場評論為什麼要查兩次 ──
// 評論的資料只有訂單 Id，後端沒有「用訂單 Id 查一筆預約」的 API。
// 所以先用訂單 Id 查訂單（getMyOrder）拿到訂單編號，再用編號搜尋我的預約（getMyReservations 的 keyword）。
//
// ── 查不到的時候 ──
// 分票、轉讓拿到票的人也能評現場，但訂單不是他的，後端回 404：用 toast 說明，不打開一個空的視窗。
//
// try/catch 的決定：這裡要處理錯誤（顯示 toast），所以 open 裡面包 try/catch；呼叫的頁面不用再包。
import { ref } from "vue";
import { useOverlay, useToast } from "@nuxt/ui/composables";
import { getMyOrder } from "@/api/orderApi";
import { getMyReservations } from "@/api/reservationApi";
import { ErrorCodes } from "@/constants/errorCodes";
import ReservationDetailModal from "@/components/reservations/ReservationDetailModal.vue";
import OrderDetailModal from "@/components/orders/OrderDetailModal.vue";

const LABELS = Object.freeze({
  visit: "查看預約詳情",
  booking: "查看訂單詳情",
});

export function useReviewDetailModals() {
  const overlay = useOverlay();
  const toast = useToast();
  const reservationModal = overlay.create(ReservationDetailModal);
  const orderModal = overlay.create(OrderDetailModal);
  const busy = ref(false);

  function label(kind) {
    if (!Object.hasOwn(LABELS, kind))
      throw new Error(`[useReviewDetailModals] 不認得的評論種類：${kind}`);
    return LABELS[kind];
  }

  async function openReservation(orderId) {
    const order = await getMyOrder(orderId);
    const { items } = await getMyReservations({ keyword: order.orderNo, pageSize: 10 });
    const reservation = items.find((r) => r.orderNo === order.orderNo);
    if (!reservation) throw new Error("找不到這筆預約的明細");
    reservationModal.open({ reservation, cancellable: false }); // 不等視窗關閉：查完資料 busy 就該解除
  }

  async function openOrder(orderId) {
    const order = await getMyOrder(orderId);
    orderModal.open({ order }); // 同上
  }

  async function open(kind, orderId) {
    if (busy.value) return;
    busy.value = true;
    try {
      const id = Number(orderId);
      if (kind === "visit") await openReservation(id);
      else if (kind === "booking") await openOrder(id);
      else throw new Error(`[useReviewDetailModals] 不認得的評論種類：${kind}`);
    } catch (err) {
      toast.add({
        title: "看不到這筆的明細",
        description:
          err.errorCode === ErrorCodes.NotFound
            ? "這筆可能是同伴幫你訂的，只有訂購的人看得到明細。"
            : err.message || "讀取明細失敗，請稍後再試",
        color: "warning",
        icon: "i-lucide-circle-alert",
      });
    } finally {
      busy.value = false;
    }
  }

  return { open, label, busy };
}
