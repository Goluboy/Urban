using NetTopologySuite.Features;
using Urban.Domain.Common;

namespace Urban.Domain.Geometry.Data;

public class BuildingBASE : GeoFeature
{
    public BuildingBASE()
    {
        
    }

    public BuildingBASE(IFeature feature) : base(feature)
    {
    }

    public string? AddrStreet { get; set; }
    public string? AddrHouseNumber { get; set; }
}