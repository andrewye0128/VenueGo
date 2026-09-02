using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class Refund
{
    public int RefundId { get; set; }

    public int PaymentId { get; set; }

    public int OriginalAmount { get; set; }

    public int DeductionRate { get; set; }

    public int DeductionAmount { get; set; }

    public int RefundAmount { get; set; }

    public byte RefundStatus { get; set; }

    public string? CancelReason { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? RefundedAt { get; set; }
}
