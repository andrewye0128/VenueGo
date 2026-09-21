using System;
using System.Collections.Generic;
using System.Linq;
using VenueGo.Models.ReservationModels;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 預約計價服務的實作（方案 C：一格時段一列明細）。
    /// <para>
    /// 整個計算就是一次 Select，沒有迴圈、沒有狀態、沒有邊界判斷。
    /// 這是選擇方案 C 最主要的好處：
    /// 若改用「按單價合併成區段」的做法，就得處理
    /// 只選一格、尖峰價等於離峰價、未來新增 PeakEndTime 後出現三段
    /// 這幾種容易寫錯的情況，而那段邏輯一旦出錯就會污染資料庫的金額。
    /// </para>
    /// </summary>
    public class ReservationPricingService : IReservationPricingService
    {
        public PricingResult Calculate(IReadOnlyList<TimeSlotStatus> slots)
        {
            if (slots is null || slots.Count == 0)
            {
                return PricingResult.Empty();
            }

            var ordered = slots.OrderBy(s => s.SlotTime).ToList();

            // 查不到價格的時段單獨列出，不要當成 0 元計入明細，
            // 否則會產生一筆金額偏低甚至為 0 的訂單。
            var withoutPrice = ordered
                .Where(s => s.UnitPrice is null)
                .Select(s => s.SlotTime)
                .ToList();

            var details = ordered
                .Where(s => s.UnitPrice.HasValue)
                .Select(s => new OrderDetailLine
                {
                    SlotTime = s.SlotTime,
                    UnitPrice = s.UnitPrice!.Value,
                    IsPeak = s.IsPeak
                })
                .ToList();

            return new PricingResult
            {
                Details = details,
                SlotsWithoutPrice = withoutPrice
            };
        }
    }
}
