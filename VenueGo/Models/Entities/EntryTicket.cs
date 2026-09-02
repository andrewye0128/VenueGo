using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class EntryTicket
{
    public int TicketId { get; set; }

    public int OrderId { get; set; }

    public string Qrtoken { get; set; } = null!;

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? UserId { get; set; }
}
