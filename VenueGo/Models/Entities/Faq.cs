using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class Faq
{
    public int FaqId { get; set; }

    public string Category { get; set; } = null!;

    public string Question { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsPublished { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }
}
