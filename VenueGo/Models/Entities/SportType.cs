using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class SportType
{
    public int SportTypeId { get; set; }

    public string SportName { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }
}
