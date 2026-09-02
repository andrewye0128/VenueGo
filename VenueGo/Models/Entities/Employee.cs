using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class Employee
{
    public int EmployeeId { get; set; }

    public int UserId { get; set; }

    public string EmployeeNo { get; set; } = null!;

    public string JobTitle { get; set; } = null!;

    public DateOnly? HireDate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
