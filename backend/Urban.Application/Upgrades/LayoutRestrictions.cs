using System.Text.Json;
using NetTopologySuite.Geometries;
using Urban.Application.Helpers;
using Urban.Application.Services;
using Urban.Domain.Geometry;

namespace Urban.Application.Upgrades
{
    public static class LayoutRestrictions
    {
        // MATCH old OknData thresholds
        private static readonly Dictionary<RestrictionType, double> thresholds = new()
        {
            { RestrictionType.CulturalHeritage_site, 200.0 },  // ОКН
            { RestrictionType.CulturalHeritage_area, 100.0 },  // Территория ОКН
            { RestrictionType.Protection_zone,        50.0 },  // Охранная зона
            { RestrictionType.Buffer_zone,            20.0 },  // Защитная зона
            { RestrictionType.Buildings,              2.0 },
            { RestrictionType.Roads,                  1.0 }
        };

        public enum RestrictionType
        {
            CulturalHeritage_site, // okn_place.json  (ОКН)
            CulturalHeritage_area, // okn_granica.json
            Protection_zone,       // okn_zona.json
            Buffer_zone,           // okn_zashit.json
            Buildings,             // buildings.json
            Roads                  // roads.json
        }

        public static readonly Dictionary<RestrictionType, string> FileMap =
            new()
            {
                { RestrictionType.CulturalHeritage_site, "okn_place.json" },
                { RestrictionType.CulturalHeritage_area, "okn_granica.json" },
                { RestrictionType.Protection_zone,       "okn_zona.json" },
                { RestrictionType.Buffer_zone,           "okn_zashit.json" },
                { RestrictionType.Buildings,             "buildings.json" },
                { RestrictionType.Roads,                 "roads.json" }
            };

        // -------------------------------------------------------
        // MAIN METHOD - consistent with OknData
        // -------------------------------------------------------
        public static List<Restriction> GetRestrictionsWithinDistance(
            Geometry target,
            params RestrictionType[] rTypes)
        {
            if (target == null || rTypes == null || rTypes.Length == 0)
                return new();

            var result = new List<Restriction>();

            foreach (var rType in rTypes)
            {
                if (!thresholds.TryGetValue(rType, out var threshold))
                    continue;

                var items = GetRestrictionsByType(rType);

                // Use < (NOT <=) to match OknData
                foreach (var r in items) 
                {
                    var distance = target.Distance(r.Geometry);
                    if (distance < threshold)
                        result.Add(r);
                }
            }

            return result;
        }

        // -------------------------------------------------------
        // Load all restrictions of a type
        // -------------------------------------------------------
        public static List<Restriction> GetRestrictionsByType(RestrictionType rType)
        {
            var path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Data",
                FileMap[rType]);

            return ReadGeojson(File.ReadAllText(path), rType);
        }

        // -------------------------------------------------------
        // FIXED GeoJSON reader — now 100% identical to OknData behavior
        // -------------------------------------------------------
        public static List<Restriction> ReadGeojson(string json, RestrictionType rType)
        {
            var list = new List<Restriction>();
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

                    // --------------------------------------------
                    // Naming logic — match OknData Russian names
                    // --------------------------------------------
                    string name = rType switch
                    {
                        RestrictionType.CulturalHeritage_site => "ОКН",
                        RestrictionType.CulturalHeritage_area => "Территория ОКН",
                        RestrictionType.Protection_zone => "Охранная зона",
                        RestrictionType.Buffer_zone => "Защитная зона",
                        RestrictionType.Buildings => "Здание",
                        RestrictionType.Roads => "Дорога",
                        _ => rType.ToString()
                    };

                    // OknData special case: ОКН get name from hintContent
                    if (rType == RestrictionType.CulturalHeritage_site &&
                        feature.TryGetProperty("properties", out var props) &&
                        props.TryGetProperty("hintContent", out var hint))
                    {
                        name = $"ОКН «{hint.GetString()}»";
                    }

                    list.Add(new Restriction
                    {
                        Geometry = geom,
                        RestrictionType = name,
                        Name = name
                    });
                }
                catch
                {
                    continue; // skip malformed feature like OknData
                }
            }

            return list;
        }
    }
}
