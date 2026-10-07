// 會員可預約的日期範圍（今天 + 最少天數 ～ 今天 + 最多天數）
// ⚠️ 要跟後端 appsettings.json 的 ReservationRules 一致：
//   MemberMinAdvanceDays、MemberMaxAdvanceDays
// 之後建議改成由後端 API 提供，避免前後端各寫一份數字、改的時候漏改
// export const MEMBER_MIN_ADVANCE_DAYS = 2;
export const MEMBER_MIN_ADVANCE_DAYS = 0;
export const MEMBER_MAX_ADVANCE_DAYS = 30;
