using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class SportTypePeakHour
{
    public int SportTypePeakHourId { get; set; }

    public int SportTypeId { get; set; }

    public byte DayOfWeek { get; set; }

    public TimeOnly? PeakStartTime { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }
}
