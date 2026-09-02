using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class Order
{
    public int OrderId { get; set; }

    public int? ReservationId { get; set; }

    public int UserId { get; set; }

    public string OrderNo { get; set; } = null!;

    public byte InvoiceType { get; set; }

    public string? CarrierNo { get; set; }

    public DateTime OrderCreatedAt { get; set; }

    public byte OrderStatus { get; set; }
}
