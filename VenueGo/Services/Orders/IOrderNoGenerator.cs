using System.Threading;
using System.Threading.Tasks;

namespace VenueGo.Services.Orders
{
    /// <summary>
    /// 訂單編號產生器。
    /// <para>
    /// 【為何獨立一個服務】訂單編號會由新增預約與訂單管理兩處產生。
    /// 若各寫一份，很容易出現格式不一致的編號
    /// （一邊 VG20260919001、另一邊 ORD-2026-0919-01），事後難以統一。
    /// 期末若新增課程報名、活動報名的訂單，也從這裡擴充前綴即可。
    /// </para>
    /// <para>
    /// 【編號格式】VG + yyyyMMdd + 當日三位序號，例如 VG20260919001。
    /// 看編號就知道是哪一天建立的第幾筆，客服接電話時能快速定位。
    /// </para>
    /// </summary>
    public interface IOrderNoGenerator
    {
        /// <summary>
        /// 產生一個新的訂單編號。
        /// <para>
        /// 【不保證絕對唯一】本方法以「查詢當日筆數再加一」的方式產生，
        /// 查詢與寫入之間存在空隙，兩人同時建單會算出相同的編號。
        /// 因此 Orders.OrderNo 的唯一鍵 UQ_Orders_OrderNo 才是最後防線：
        /// 呼叫端必須接住唯一鍵衝突並重新呼叫本方法重試。
        /// </para>
        /// </summary>
        Task<string> GenerateAsync(CancellationToken cancellationToken = default);
    }
}