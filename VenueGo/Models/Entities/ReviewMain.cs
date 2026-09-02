using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class ReviewMain
{
    public int ReviewId { get; set; }

    public int? ReviewPerBookingId { get; set; }

    public int? ReviewPerVisitId { get; set; }

    public int? UserId { get; set; }

    public byte StarRating { get; set; }

    public string? ReviewContent { get; set; }

    public bool IsAnonymous { get; set; }

    public bool IsPublic { get; set; }

    public bool MentionsVenue { get; set; }

    public bool MentionsStaff { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public int? ReadByEmployeeId { get; set; }

    public string? ReplyContent { get; set; }

    public DateTime? RepliedAt { get; set; }

    public int? RepliedByEmployeeId { get; set; }
}
