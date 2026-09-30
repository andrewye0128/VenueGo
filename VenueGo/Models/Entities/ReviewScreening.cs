using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class ReviewScreening
{
    public int ReviewId { get; set; }

    public bool IsPriority { get; set; }

    public byte AiStatus { get; set; }

    public byte AiAttempts { get; set; }

    public DateTime? AiAnalyzedAt { get; set; }

    public string? AiModel { get; set; }

    public string? Summary { get; set; }

    public byte? NegativeIntensity { get; set; }

    public bool NeedsManager { get; set; }

    public string? ManagerReason { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public int? VerifiedByEmployeeId { get; set; }

    public byte? VerifyResult { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? NextAttemptAt { get; set; }
}
