using System;
using System.Collections.Generic;

namespace VenueGo.Models.Entities;

public partial class ReviewScreeningLabel
{
    public int ReviewId { get; set; }

    public byte LabelType { get; set; }

    public byte LabelCode { get; set; }

    public byte Source { get; set; }
}
