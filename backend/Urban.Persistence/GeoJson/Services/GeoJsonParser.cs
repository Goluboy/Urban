using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Urban.Domain.Common;

namespace Urban.Persistence.GeoJson.Services;

public static class GeoJsonParser
{
    public static List<GeoFeature> ParseGeoJson(string geoJson)
    {
        var reader = new GeoJsonReader();
        var features = reader.Read<FeatureCollection>(geoJson);

        return features.Select(f => new GeoFeature
        {
            Geometry = (Polygon)f.Geometry,
            Properties = f.Attributes?.GetNames()
                .ToDictionary(name => name, name => f.Attributes.GetOptionalValue(name))
        }).ToList();
    }
}