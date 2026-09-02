using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class SportTypePriceRule
{
    public int SportTypePriceRuleId { get; set; }

    public int SportTypeId { get; set; }

    public TimeOnly? PeakStartTime { get; set; }

    public int PeakPrice { get; set; }

    public int OffPeakPrice { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }
}
