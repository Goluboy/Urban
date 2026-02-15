using Urban.Domain.Common;

namespace Urban.Domain.Geometry;

public class BuildlingBASE : GeoFeature
{
    public string? AddrStreet { get; set; }
    public string? AddrHouseNumber { get; set; }
}