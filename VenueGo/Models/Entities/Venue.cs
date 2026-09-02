using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class Venue
{
    public int VenueId { get; set; }

    public string VenueName { get; set; } = null!;

    public int SportTypeId { get; set; }

    public string Location { get; set; } = null!;

    public bool IsActive { get; set; }

    public int? Capacity { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }
}
