using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class ReservationSlot
{
    public int ReservationSlotId { get; set; }

    public int ReservationId { get; set; }

    public int VenueId { get; set; }

    public DateOnly BookingDate { get; set; }

    public TimeOnly SlotStartTime { get; set; }
}
