using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class VReservationSummary
{
    public int ReservationId { get; set; }

    public int UserId { get; set; }

    public string? UserName { get; set; }

    public int VenueId { get; set; }

    public string? VenueName { get; set; }

    public int? SportTypeId { get; set; }

    public string? SportName { get; set; }

    public DateOnly BookingDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int? OrderId { get; set; }

    public string? OrderNo { get; set; }

    public byte? OrderStatusValue { get; set; }

    public int? OrderPersonMount { get; set; }

    public int? OrderTotalAmount { get; set; }

    public DateTime? OrderCreatedAt { get; set; }

    public DateTime? PaymentPaidAt { get; set; }

    public byte? PaymentMethodValue { get; set; }

    public int? TicketCount { get; set; }

    public int? CheckedInCount { get; set; }

    public decimal? CheckInRate { get; set; }
}
