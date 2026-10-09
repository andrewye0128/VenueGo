using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class News
{
    public int NewsId { get; set; }

    public string Category { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string ImagePath { get; set; } = null!;

    public string? CoverTitle { get; set; }

    public string? CoverTitleStyles { get; set; }

    public string? CoverText { get; set; }

    public string? CoverTextStyles { get; set; }

    public DateTime PublishedAt { get; set; }

    public bool IsPublished { get; set; }

    public bool IsPinned { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }
}
