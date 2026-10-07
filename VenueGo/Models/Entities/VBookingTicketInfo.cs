using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class VBookingTicketInfo
{
    public int UserId { get; set; }

    public string? UserName { get; set; }

    public string? UserPhone { get; set; }

    public int IsOrphanUser { get; set; }

    public int? SportTypeId { get; set; }

    public string? SportName { get; set; }

    public int VenueId { get; set; }

    public string? VenueName { get; set; }

    public int? VenueCapacity { get; set; }

    public int ReservationId { get; set; }

    public DateOnly BookingDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int? OrderId { get; set; }

    public string? OrderNo { get; set; }

    public int? OrderPersonMount { get; set; }

    public int? OrderTotalAmount { get; set; }

    public byte? OrderStatusValue { get; set; }

    public string? OrderStatusName { get; set; }

    public int? PaymentId { get; set; }

    public byte? PaymentStatusValue { get; set; }

    public byte? PaymentMethodValue { get; set; }

    public string? PaymentMethodName { get; set; }

    public DateTime? PaymentPaidAt { get; set; }

    public int? TicketId { get; set; }

    public int? TicketUserId { get; set; }

    public string? TicketQrtoken { get; set; }

    public byte? TicketStatusValue { get; set; }

    public string? TicketStatusName { get; set; }

    public int? LastLogId { get; set; }

    public byte? LastActionValue { get; set; }

    public string? LastActionName { get; set; }

    public DateTime? LastCheckInTime { get; set; }

    public bool? LastIsManualOverride { get; set; }
}
