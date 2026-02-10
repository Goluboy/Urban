using NetTopologySuite.Geometries;

namespace Urban.Domain.Common;

public class GeoFeature
{
    public string? Name { get; set; }
    public Polygon Geometry { get; set; } = null!;
    public Dictionary<string, object>? Properties { get; set; }
}