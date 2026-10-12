// 預約頁「選時段」的規則（純函式，不碰畫面）
// 規則跟後端 SlotSelectionValidator 一致：同一個場地、連續、最多 MAX 格
// ⚠️ 這裡只是讓畫面好操作；送出時後端會重新驗證一次，不能只靠前端
//
// selection 的格式：{ venueId: 7, slotTimes: ["18:00", "19:00"] }（已排序）；沒選是 null
//
// 點擊規則：
//   沒選            → 選這一格
//   點不同場地       → 改選新場地的這一格
//   接在最後／最前面  → 延長（超過上限時不動，回傳提示）
//   點最後／最前那格  → 取消那一格（只剩一格時就全部取消）
//   點中間那格       → 不動，回傳提示
//   不相鄰          → 重新開始，改選這一格（使用者通常是想改時間，不跳錯誤）

import { SLOT_AVAILABILITY } from "@/constants/statuses";

/**
 * 這一格能不能選：狀態是「可預約」，而且有價格
 * 後端查不到計價規則時 unitPrice 是 null：選了也算不出金額，所以當成不能選（顯示「價格未設定」）
 * 時段按鈕、場地卡片的「剩幾格」都用這個函式，兩邊的規則才會一致
 */
export const isSelectableSlot = (slot) =>
  slot.availability === SLOT_AVAILABILITY.AVAILABLE && slot.unitPrice != null;

const toHour = (slotTime) => Number(slotTime.slice(0, 2));
const toSlotTime = (hour) => `${String(hour).padStart(2, "0")}:00`;

/**
 * @param {{ venueId: number, slotTimes: string[] } | null} selection 目前的選擇
 * @param {number} venueId 點的場地
 * @param {string} slotTime 點的時段 "18:00"
 * @param {number} maxSlots 最多幾格
 * @returns {{ selection: object | null, message?: string }} 新的選擇；message 是要提示使用者的話
 */
export function toggleSlot(selection, venueId, slotTime, maxSlots) {
  const single = { selection: { venueId, slotTimes: [slotTime] } };
  if (!selection || selection.venueId !== venueId || selection.slotTimes.length === 0) {
    return single;
  }

  const hours = selection.slotTimes.map(toHour);
  const first = hours[0];
  const last = hours[hours.length - 1];
  const hour = toHour(slotTime);

  // 點已選的格子
  if (hours.includes(hour)) {
    if (hours.length === 1) return { selection: null };
    if (hour === first || hour === last) {
      const rest = hours.filter((h) => h !== hour);
      return { selection: { venueId, slotTimes: rest.map(toSlotTime) } };
    }
    return { selection, message: "只能取消最前或最後一個時段" };
  }

  // 接在前面或後面：延長
  if (hour === last + 1 || hour === first - 1) {
    if (hours.length >= maxSlots) {
      return { selection, message: `一次最多選 ${maxSlots} 小時` };
    }
    const next = [...hours, hour].sort((a, b) => a - b);
    return { selection: { venueId, slotTimes: next.map(toSlotTime) } };
  }

  // 不相鄰：重新開始
  return single;
}

// 選擇的結束時間："19:00" → "20:00"（每格一小時）
export const slotEndTime = (slotTime) => toSlotTime(toHour(slotTime) + 1);
