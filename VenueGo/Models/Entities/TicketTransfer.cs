using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class TicketTransfer
{
    public int TransferId { get; set; }

    public int TicketId { get; set; }

    public int FromUserId { get; set; }

    public int? ToUserId { get; set; }

    public string TransferCode { get; set; } = null!;

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
