using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class TicketStatusLog
{
    public int LogId { get; set; }

    public int TicketId { get; set; }

    public byte ActionType { get; set; }

    public byte FromStatus { get; set; }

    public byte ToStatus { get; set; }

    public byte? ReasonType { get; set; }

    public string? Reason { get; set; }

    public int? OperatorId { get; set; }

    public DateTime CreatedAt { get; set; }
}
