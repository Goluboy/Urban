using NetTopologySuite.Geometries;
using System.Text.Json;

namespace Urban.Domain.Common;

public class GeoFeature : BaseEntity
{
    public Polygon Geometry { get; set; } = null!;
    public Dictionary<string, object>? Properties { get; set; }
}