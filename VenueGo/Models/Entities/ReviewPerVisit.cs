using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class ReviewPerVisit
{
    public int ReviewPerVisitId { get; set; }

    public int TicketId { get; set; }

    public int OrderId { get; set; }

    public int UserId { get; set; }

    public int VenueId { get; set; }

    public int SportTypeId { get; set; }

    public DateTime RentStartTime { get; set; }

    public DateTime RentEndTime { get; set; }

    public DateTime? ActualEndTime { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiredAt { get; set; }
}
