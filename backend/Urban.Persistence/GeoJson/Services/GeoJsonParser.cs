using Urban.Domain.Common;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Urban.Persistence.GeoJson.Services;

public static class GeoJsonParser
{
    public static List<GeoFeature> ParseGeoJson(string geoJson)
    {
        var reader = new GeoJsonReader();
        var features = reader.Read<FeatureCollection>(geoJson);

        return features.Select(f => new GeoFeature
        {
            Name = f.Attributes?.ContainsKey("name") == true ? f.Attributes["name"].ToString() : null,
            Geometry = (Polygon)f.Geometry,
            Properties = f.Attributes?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        }).ToList();
    }
}