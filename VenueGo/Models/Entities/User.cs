using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class User
{
    public int UserId { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateOnly Birth { get; set; }

    public string? CarrierNo { get; set; }

    public int CumulativeConsumption { get; set; }

    public int CumulativeVisitTime { get; set; }

    public int FailedLoginCount { get; set; }

    public int NoShowCount { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
