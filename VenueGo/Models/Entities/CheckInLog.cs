using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class CheckInLog
{
    public int LogId { get; set; }

    public int TicketId { get; set; }

    public byte Action { get; set; }

    public DateTime ActionTime { get; set; }

    public bool IsValid { get; set; }

    public bool IsManualOverride { get; set; }

    public int? OperatorId { get; set; }
}
