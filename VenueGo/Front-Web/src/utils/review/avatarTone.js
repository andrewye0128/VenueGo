// 評論卡頭像的顏色（昱，10/13）
// 同一個名字永遠拿到同一個顏色：把名字的每個字的字碼加起來，對顏色數量取餘數。
// 不用亂數：重新整理之後顏色會跳，同一個人在兩張卡片上顏色也會不一樣。
import { AVATAR_TONES } from "@/constants/review";

/** 名字 → 頭像的 class（底色＋文字色） */
export function avatarTone(name) {
  const text = name ?? "";
  let sum = 0;
  for (const ch of text) sum += ch.codePointAt(0);
  return AVATAR_TONES[sum % AVATAR_TONES.length];
}

/** 頭像上顯示的字：名字的第一個字（emoji、罕用字也不會被切成半個） */
export function avatarInitial(name) {
  const first = [...(name ?? "")][0];
  return first ?? "?";
}
