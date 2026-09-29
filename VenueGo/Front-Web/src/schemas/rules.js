// 共用的表單欄位驗證規則（Zod）
// 各表單的 schema 由這裡的規則組合而成；改規則只要改這裡，所有表單一起更新
// 錯誤訊息盡量與後端 C# 的 DataAnnotations（例如 [Required]、[StringLength]）一致
import { z } from "zod";

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

// 必須勾選（例如同意使用條款）
export const mustAgreeRule = (message) => z.literal(true, { error: message });
