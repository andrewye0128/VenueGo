// 共用的表單欄位驗證規則（Zod）
// 各表單的 schema 由這裡的規則組合而成；改規則只要改這裡，所有表單一起更新
// 錯誤訊息盡量與後端 C# 的 DataAnnotations（例如 [Required]、[StringLength]）一致
import { z } from "zod";
import { ALL_SPORTS } from "@/constants/sportTypes";

// 姓名：必填、最多 50 字（對應後端 [StringLength(50)]）
export const nameRule = z.string().trim().min(1, "請輸入姓名").max(50, "姓名長度不能超過 50 個字");

// Email：必填 + 格式
// .pipe()：前一關（必填）沒過就停下，不會同時出現「請輸入」和「格式不正確」兩則訊息
export const emailRule = z.string().trim().min(1, "請輸入 Email").pipe(z.email("Email 格式不正確"));

// 台灣手機：必填 + 09 開頭共 10 碼
export const mobileRule = z
  .string()
  .trim()
  .min(1, "請輸入手機號碼")
  .pipe(z.string().regex(/^09\d{8}$/, "手機號碼格式不正確（例：0912345678）"));

// 運動類型（搭配 SportTypeSelect）：值是 sportTypeId（數字），還沒選是 ""
export const sportTypeRule = z.number({ error: "請選擇運動類型" });

// 運動類型，可以選「全部運動」（SportTypeSelect 加 include-all 時用）
// 值是 sportTypeId（數字）或 ALL_SPORTS（"all"）
export const sportTypeOrAllRule = z.union([z.number(), z.literal(ALL_SPORTS)], {
  error: "請選擇運動類型",
});

// 必須勾選（例如同意使用條款）
export const mustAgreeRule = (message) => z.literal(true, { error: message });

// 密碼：8～100 字元，且同時包含小寫、大寫英文與數字（對應後端 RegisterMemberRequestDto 的 [StringLength][RegularExpression]）
// 每條規則各自一則訊息，使用者可以知道缺什麼
export const passwordRule = z
  .string()
  .min(8, "密碼至少需要 8 個字元")
  .max(100, "密碼不可超過 100 個字元")
  .regex(/[a-z]/, "密碼需包含小寫英文字母")
  .regex(/[A-Z]/, "密碼需包含大寫英文字母")
  .regex(/[0-9]/, "密碼需包含數字");

// 今天的日期字串（YYYY-MM-DD，本機時區）。日期字串可以直接比大小
function todayString() {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");
  return `${now.getFullYear()}-${month}-${day}`;
}

// 生日（YYYY-MM-DD）：必填、必須是真的存在的日期（沒有 2/30）、不早於 1900 年、不能是未來
// 用 superRefine 一次只回報一則最相關的訊息，不會同時出現「不存在」與「晚於今天」
export const birthRule = z
  .string()
  .min(1, "請選擇出生日期")
  .superRefine((value, ctx) => {
    if (value === "") return; // 空白已經由上面的「請選擇出生日期」處理

    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
    const [year, month, day] = match ? match.slice(1).map(Number) : [];
    const date = match ? new Date(year, month - 1, day) : null;
    const exists =
      date &&
      date.getFullYear() === year &&
      date.getMonth() === month - 1 &&
      date.getDate() === day;

    let message = "";
    if (!exists) message = "請輸入正確的日期";
    else if (value > todayString()) message = "生日不可晚於今天";
    else if (value < "1900-01-01") message = "生日不可早於 1900 年";

    if (message) ctx.addIssue({ code: "custom", message });
  });

// 手機載具（選填）：空白或「/」加 7 碼英數字與 . + -（後端會統一轉大寫）
export const carrierNoRule = z
  .string()
  .trim()
  .refine(
    (value) => value === "" || /^\/[A-Za-z0-9.+-]{7}$/.test(value),
    "載具格式為「/」加上 7 碼英數字或符號",
  );
