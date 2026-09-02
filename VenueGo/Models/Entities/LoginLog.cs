using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class LoginLog
{
    public int LoginLogId { get; set; }

    public int? UserId { get; set; }

    public string LoginAccount { get; set; } = null!;

    public string IpAddress { get; set; } = null!;

    public DateTime LoginTime { get; set; }

    public bool Result { get; set; }

    public string? FailureReason { get; set; }
}
