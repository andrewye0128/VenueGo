using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class VReviewAdmin
{
    public int ReviewId { get; set; }

    public byte ReviewKind { get; set; }

    public int UserId { get; set; }

    public string? MemberName { get; set; }

    public int OrderId { get; set; }

    public string? OrderNo { get; set; }

    public int? TicketId { get; set; }

    public byte StarRating { get; set; }

    public string? ReviewContent { get; set; }

    public bool IsAnonymous { get; set; }

    public string? AnonymousNickname { get; set; }

    public bool IsPublic { get; set; }

    public bool MentionsVenue { get; set; }

    public bool MentionsStaff { get; set; }

    public bool IsPinned { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public int? ReadByEmployeeId { get; set; }

    public string? ReplyContent { get; set; }

    public DateTime? RepliedAt { get; set; }

    public int? RepliedByEmployeeId { get; set; }

    public DateTime? ReplyViewedAt { get; set; }

    public byte? ReplySatisfaction { get; set; }

    public DateTime? SpamMarkedAt { get; set; }

    public byte? SpamReason { get; set; }

    public int? SpamMarkedByEmployeeId { get; set; }

    public int? ReservationId { get; set; }

    public int? VenueId { get; set; }

    public string? VenueName { get; set; }

    public int? SportTypeId { get; set; }

    public string? SportName { get; set; }

    public DateTime? RentStartTime { get; set; }

    public DateTime? RentEndTime { get; set; }

    public DateTime? CheckInAt { get; set; }

    public DateTime? CheckOutAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public byte? PaymentChannel { get; set; }

    public string? ReadByEmployeeName { get; set; }

    public string? RepliedByEmployeeName { get; set; }

    public string? SpamMarkedByEmployeeName { get; set; }
}
