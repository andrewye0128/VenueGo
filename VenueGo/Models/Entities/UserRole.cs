using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class UserRole
{
    public int UserId { get; set; }

    public int RoleId { get; set; }

    public int? AssignedBy { get; set; }

    public DateTime AssignedAt { get; set; }
}
