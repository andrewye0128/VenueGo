using Microsoft.AspNetCore.Mvc;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.Models.ReservationModels;
using VenueGo.ViewModels;

namespace VenueGo.Controllers
{
    public class ReservationController : Controller
    {
        public IActionResult Index()
        {
            //dbVenueContext db = new dbVenueContext();
            //IEnumerable<Reservation> datas = from t in db.Reservations
            //                                 select t;
            //return View(datas);

            dbVenueContext db = new dbVenueContext();

            //List<CReservationWrap> datas = new List<CReservationWrap>();
            //foreach (var item in db.Reservations)
            //{
            //    datas.Add(new CReservationWrap() { reservation = item });
            //}

            var datas = (from r in db.Reservations
                         join u in db.Users on r.UserId equals u.UserId
                         join v in db.Venues on r.VenueId equals v.VenueId

                         join o in db.Orders on r.ReservationId equals o.ReservationId into og
                         from o in og.DefaultIfEmpty()

                         join p in db.Payments on o.OrderId equals p.OrderId into pg
                         from p in pg.DefaultIfEmpty()

                         select new ReservationListViewModel
                         {
                             ReservationId = r.ReservationId,
                             UserName = u.Name,
                             VenueName = v.VenueName,
                             BookingDate = r.BookingDate,
                             StartTime = r.StartTime,
                             EndTime = r.EndTime,
                             ReservationStatus = r.ReservationStatus,
                             PaymentStatus = p == null ? (byte?)null : p.PaymentStatus
                         }).ToList();

            return View(datas);
        }


        public IActionResult Create() 
        {
            return View();
        }
    }
}
