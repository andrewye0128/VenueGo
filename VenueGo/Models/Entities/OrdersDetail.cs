using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class OrdersDetail
{
    public int OrderDetailId { get; set; }

    public int OrderId { get; set; }

    public int? ReservationId { get; set; }

    public int UnitPrice { get; set; }

    public int DurationHours { get; set; }

    public int PersonMount { get; set; }
}
