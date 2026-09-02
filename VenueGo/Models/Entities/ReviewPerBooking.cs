using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class ReviewPerBooking
{
    public int ReviewPerBookingId { get; set; }

    public int UserId { get; set; }

    public int SourceId { get; set; }

    public byte PaymentMethod { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiredAt { get; set; }
}
