using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class VReviewFullInfo
{
    public int ReviewId { get; set; }

    public int? ReviewPerVisitId { get; set; }

    public int? ReviewPerBookingId { get; set; }

    public bool? IsBookingReview { get; set; }

    public int? OrderId { get; set; }

    public string? OrderNo { get; set; }

    public string? Qrtoken { get; set; }

    public int? VenueId { get; set; }

    public string? VenueName { get; set; }

    public string? SportName { get; set; }

    public DateTime? RentStartTime { get; set; }

    public string? UserName { get; set; }

    public string? ReadByEmployeeName { get; set; }

    public string? RepliedByEmployeeName { get; set; }

    public string? SpamMarkedByEmployeeName { get; set; }
}
