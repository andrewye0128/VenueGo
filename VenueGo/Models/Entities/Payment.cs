using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public int Amount { get; set; }

    public byte PaymentMethod { get; set; }

    public byte PaymentChannel { get; set; }

    public byte PaymentStatus { get; set; }

    public DateTime PaymentDueAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? TransactionNo { get; set; }

    public DateTime CreatedAt { get; set; }
}
