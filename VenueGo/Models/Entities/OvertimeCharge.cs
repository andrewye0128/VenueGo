using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class OvertimeCharge
{
    public int OvertimeChargeId { get; set; }

    public int OrderId { get; set; }

    public int? TicketId { get; set; }

    public int OverdueMinutes { get; set; }

    public int UnitPrice { get; set; }

    public decimal BillingHours { get; set; }

    public int ChargeAmount { get; set; }

    public byte Status { get; set; }

    public byte? PaymentMethod { get; set; }

    public string? Remark { get; set; }

    public int? OperatorId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }
}
