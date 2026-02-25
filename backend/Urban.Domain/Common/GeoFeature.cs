using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using Urban.Domain.Geometry.Data;

namespace Urban.Domain.Common;

public class GeoFeature : BaseEntity, IFeature
{
    public GeoFeature()
    { }

    public GeoFeature(IFeature feature)
    {
        Geometry = feature.Geometry;
        BoundingBox = feature.BoundingBox;
        Attributes = feature.Attributes;
        Properties = feature.Attributes?.GetNames()
            .ToDictionary(
                name => name,
                name => feature.Attributes.GetOptionalValue(name)
            );
        GeometryType = feature.Geometry?.GeometryType switch
        {
            "Point" => GeometryType.Point,
            "Polygon" => GeometryType.Polygon,
            "MultiPolygon" => GeometryType.MultiPolygon,
            "LineString" => GeometryType.LineString,

            _ => throw new NotSupportedException($"Unsupported geometry type: {feature.Geometry?.GeometryType}")
        };
    }

    public NetTopologySuite.Geometries.Geometry? Geometry { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
    [NotMapped]
    public Envelope? BoundingBox { get; set; }
    [NotMapped]
    public IAttributesTable? Attributes { get; set; }
    [NotMapped]
    public GeometryType GeometryType { get; set; }
}