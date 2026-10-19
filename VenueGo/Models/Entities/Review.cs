using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class Review
{
    public int ReviewId { get; set; }

    public byte ReviewKind { get; set; }

    public int UserId { get; set; }

    public int? TicketId { get; set; }

    public int OrderId { get; set; }

    public byte StarRating { get; set; }

    public string? ReviewContent { get; set; }

    public bool IsAnonymous { get; set; }

    public bool IsPublic { get; set; }

    public bool MentionsVenue { get; set; }

    public bool MentionsStaff { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? AnonymousNickname { get; set; }

    public DateTime? ReadAt { get; set; }

    public int? ReadByEmployeeId { get; set; }

    public bool IsPinned { get; set; }

    public string? ReplyContent { get; set; }

    public DateTime? RepliedAt { get; set; }

    public int? RepliedByEmployeeId { get; set; }

    public DateTime? ReplyViewedAt { get; set; }

    public byte? ReplySatisfaction { get; set; }

    public DateTime? SpamMarkedAt { get; set; }

    public byte? SpamReason { get; set; }

    public int? SpamMarkedByEmployeeId { get; set; }
}
