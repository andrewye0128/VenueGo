using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class ReviewPerVisit
{
    public int ReviewPerVisitId { get; set; }

    public string Qrtoken { get; set; } = null!;

    public int BookingMemberId { get; set; }

    public int VenueId { get; set; }

    public int SportTypeId { get; set; }

    public DateTime RentStartTime { get; set; }

    public DateTime RentEndTime { get; set; }

    public DateTime? ActualEndTime { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiredAt { get; set; }
}
