using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using System.Text.Json;
using Urban.Application.Helpers;
using Urban.Domain.Common;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Urban.Persistence.GeoJson.Services;

public static class GeoJsonParser
{
    public static List<GeoFeature> ParseGeoJson(string geoJson)
    {
        try
        {
            var reader = new GeoJsonReader();
            var features = reader.Read<FeatureCollection>(geoJson);

            return features.Select(f => new GeoFeature(f)).ToList();
        }
        catch (JsonReaderException ex)
        {
            return ReadGeoJson(geoJson); // Handle unclosed ring in json
        }
    }

    public static List<GeoFeature> ReadGeoJson(string json)
    {
        var list = new List<GeoFeature>();
        var root = JsonSerializer.Deserialize<JsonElement>(json);
        var factory = GeometryFactory.Default;

        var features = root.GetProperty("features");

        foreach (var feature in features.EnumerateArray())
        {
            try
            {
                var geomNode = feature.GetProperty("geometry");
                var gType = geomNode.GetProperty("type").GetString();
                var coords = geomNode.GetProperty("coordinates");

                Geometry geom = null;

                // -----------------------
                // Polygon
                // -----------------------
                if (gType == "Polygon")
                {
                    var rings = coords.EnumerateArray()
                        .Select(ring => ring
                            .EnumerateArray()
                            .Select(c => new Coordinate(c[1].GetDouble(), c[0].GetDouble()))
                            .ToArray())
                        .ToArray();

                    if (rings.Length == 0 || rings[0].Length < 3)
                        continue;

                    // close ring (OknData logic)
                    var shell = factory.CreateLinearRing(
                        rings[0].Append(rings[0][0]).ToArray());

                    var holes = rings
                        .Skip(1)
                        .Select(r => factory.CreateLinearRing(r))
                        .ToArray();

                    geom = factory.CreatePolygon(shell, holes);
                }
                // -----------------------
                // Point
                // -----------------------
                else if (gType == "Point")
                {
                    var c = coords.EnumerateArray().ToArray();
                    geom = new Point(new Coordinate(c[1].GetDouble(), c[0].GetDouble()));
                }
                else
                {
                    Console.WriteLine(gType);
                    continue;
                }

                // --------------------------------------------
                // UTM conversion EXACTLY like OknData
                // --------------------------------------------
                if (geom is Polygon p)
                {
                    var (converted, _) = CoordinatesConverter.ToUtm(p);
                    geom = converted;
                }
                else if (geom is Point pt)
                {
                    var (converted, _) = CoordinatesConverter.ToUtm(pt);
                    geom = converted;
                }

                list.Add(new GeoFeature(new Feature(geom, new AttributesTable(JsonConvert.DeserializeObject<Dictionary<string, object>>(json)))));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing feature: {ex.Message}");
            }
        }

        return list;
    }
}