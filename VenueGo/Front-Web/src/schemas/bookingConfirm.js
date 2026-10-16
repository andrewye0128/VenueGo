// 填寫資料頁（/booking/confirm）的表單驗證規則
// 前端檢查是為了讓客人馬上看到哪裡填錯；真正的把關在後端（人數上限、載具格式、條款都會再檢查一次）
import { z } from "zod";
import { mustAgreeRule } from "@/schemas/rules";
import { CARRIER_NO_PATTERN, INVOICE_TYPE } from "@/constants/payment";

// 手機條碼載具：先去掉空白、轉大寫再檢查（後端只接受大寫，客人打小寫也幫他轉好）
const carrierNoRule = z
  .string()
  .trim()
  .toUpperCase()
  .min(1, "請輸入手機條碼載具")
  .regex(CARRIER_NO_PATTERN, "載具格式為斜線加 7 碼英數字，例如 /AB12345");

/**
 * 依這次的試算結果與目前的選擇產生驗證規則
 * 人數上限、能選的付款方式、要不要檢查載具每次不一樣，所以寫成函式，頁面用 computed 呼叫
 *
 * 為什麼載具不寫成「整張表單的檢查」（superRefine）：
 *   Zod 的整張表單檢查，要等其他欄位都正確才會執行，
 *   客人還沒勾條款時，載具填錯也不會顯示錯誤。改成依目前的發票類型決定載具欄位的規則，
 *   載具欄位就能自己檢查、離開欄位時馬上顯示錯誤
 * @param {object} options
 * @param {number} options.maxPerson 使用人數上限（場地的容納人數）
 * @param {number[]} options.paymentMethods 能選的付款方式
 * @param {number} options.invoiceType 目前選的發票類型：選手機條碼時載具必填
 */
export function createBookingConfirmSchema({ maxPerson, paymentMethods, invoiceType }) {
  return z.object({
    personAmount: z
      .number({ error: "請輸入使用人數" })
      .int("人數必須是整數")
      .min(1, "使用人數至少 1 人")
      .max(maxPerson, `使用人數最多 ${maxPerson} 人`),
    paymentMethod: z
      .number({ error: "請選擇付款方式" })
      .refine((value) => paymentMethods.includes(value), "請選擇付款方式"),
    invoiceType: z.number(),
    // 紙本發票：載具不檢查（輸入框藏起來了，送出時也不會送）
    carrierNo: invoiceType === INVOICE_TYPE.MOBILE_BARCODE ? carrierNoRule : z.string(),
    agree: mustAgreeRule("請先閱讀並同意租借條款"),
  });
}
