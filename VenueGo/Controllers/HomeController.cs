using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using VenueGo.Data;
using VenueGo.Models;

namespace VenueGo.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        // 測試 EF Core 是否建立
        public IActionResult TestDb()
        {
            try
            {
                using dbVenueContext db =
                    new dbVenueContext();

                int count =
                    db.Reservations.Count();

                return Content(
                    $"資料庫連線成功！Reservations 目前共有 {count} 筆資料。"
                );
            }
            catch (Exception ex)
            {
                return Content(
                    $"資料庫連線失敗：{ex.Message}"
                );
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
