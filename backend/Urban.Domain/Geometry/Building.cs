using Urban.Domain.Common;

namespace Urban.Domain.Geometry;

public class Building : GeoFeature
{
    public required NetTopologySuite.Geometries.Geometry Geometry { get; set; }
}