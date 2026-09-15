using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class Reservation
{
    public int ReservationId { get; set; }

    public int UserId { get; set; }

    public int VenueId { get; set; }

    public DateOnly BookingDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public DateTime ReservedAt { get; set; }

    public byte ReservationStatus { get; set; }

    public DateTime PaymentDueAt { get; set; }

    public DateTime TermsAcceptedAt { get; set; }

    public string TermsVersion { get; set; } = null!;

    public int? CreatedBy { get; set; }

    public byte Source { get; set; }

    public int? CancelledBy { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancelReason { get; set; }
}
