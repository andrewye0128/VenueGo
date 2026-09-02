using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class WeekBusinessHour
{
    public int BusinessHoursId { get; set; }

    public byte DayOfWeek { get; set; }

    public bool IsOpen { get; set; }

    public TimeOnly? OpenTime { get; set; }

    public TimeOnly? CloseTime { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }
}
