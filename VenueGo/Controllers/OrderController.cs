using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Services;

namespace VenueGo.Controllers
{
    public class OrderController : Controller
    {
        private readonly dbVenueContext _db;
        private readonly IEntryTicketService _entryTicketService;

        public OrderController(dbVenueContext db, IEntryTicketService entryTicketService)
        {
            _db = db;
            _entryTicketService = entryTicketService;
        }

        [HttpGet]
        public async Task<IActionResult> Confirm(int orderId)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound($"找不到訂單{orderId}");

            order.OrderStatus = 1;

            var isCreateTicket = await _entryTicketService.CreateForOrderAsync(orderId);
            if (isCreateTicket.TicketCount == 0)
            {
                return Content(isCreateTicket.Message);
            }

            await _db.SaveChangesAsync();   // 模擬階段這裡統一存
            return Content($"訂單{orderId} 已成立，{isCreateTicket.TicketCount}張票券已建立");
        }

        // GET /Order/Cancel?orderId=1
        [HttpGet]
        public async Task<IActionResult> Cancel(int orderId)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound($"找不到訂單{orderId}");

            order.OrderStatus = 2;   // 2 使用者取消（後台取消 = 5）

            int cancelled = await _entryTicketService.CancelByOrderAsync(orderId);

            await _db.SaveChangesAsync();
            return Content($"訂單{orderId} 已取消，連動失效{cancelled} 張票券");
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}