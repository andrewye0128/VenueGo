using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class RolePermission
{
    public int RoleId { get; set; }

    public int PermissionId { get; set; }

    public bool Status { get; set; }

    public DateTime AssignedAt { get; set; }

    public int? AssignedBy { get; set; }
}
