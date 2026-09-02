using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class VenueUnavailableSlot
{
    public int VenueUnavailableSlotId { get; set; }

    public int VenueId { get; set; }

    public DateOnly UnavailableDate { get; set; }

    public TimeOnly UnavailableStartTime { get; set; }

    public string Reason { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }
}
