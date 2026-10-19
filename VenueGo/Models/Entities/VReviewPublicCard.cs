using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class VReviewPublicCard
{
    public int ReviewId { get; set; }

    public byte StarRating { get; set; }

    public string? ReviewContent { get; set; }

    public bool IsAnonymous { get; set; }

    public string? AnonymousNickname { get; set; }

    public string MemberName { get; set; } = null!;

    public bool MentionsVenue { get; set; }

    public bool MentionsStaff { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public string? ReplyContent { get; set; }

    public DateTime? RepliedAt { get; set; }

    public DateTime? ReplyViewedAt { get; set; }

    public int VenueId { get; set; }

    public string VenueName { get; set; } = null!;

    public int SportTypeId { get; set; }

    public string SportName { get; set; } = null!;

    public DateTime? CheckInAt { get; set; }
}
