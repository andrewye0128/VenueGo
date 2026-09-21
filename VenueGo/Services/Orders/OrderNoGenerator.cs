using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;

namespace VenueGo.Services.Orders
{
    /// <summary>
    /// 訂單編號產生器的實作。
    /// </summary>
    public class OrderNoGenerator : IOrderNoGenerator
    {
        /// <summary>系統代號前綴。期末若有課程或活動訂單，可在此擴充其他前綴。</summary>
        private const string Prefix = "VG";

        /// <summary>當日序號的位數。三位可容納 999 筆，對單一運動中心遠遠足夠。</summary>
        private const int SequenceDigits = 3;

        private readonly dbVenueContext _db;

        public OrderNoGenerator(dbVenueContext db)
        {
            _db = db;
        }

        public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
        {
            var datePart = $"{Prefix}{DateTime.Now:yyyyMMdd}";

            // 每次呼叫都重新查詢。重試時才會看到對方剛寫進去的那一筆，
            // 否則重試也只會算出同樣的編號。
            var count = await _db.Orders.AsNoTracking()
                .CountAsync(o => o.OrderNo.StartsWith(datePart), cancellationToken);

            var sequence = (count + 1).ToString($"D{SequenceDigits}");

            return $"{datePart}{sequence}";
        }
    }
}