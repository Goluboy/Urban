using System.Text.Json;
using NetTopologySuite.Geometries;
using Urban.Application.Helpers;

namespace Urban.Application.Services
{
    public static class OknData
    {
        private static Dictionary<string, List<Restriction>> _restrictionsByType = null;
        
        // Distance thresholds for each restriction type (in meters)
        private static readonly Dictionary<string, double> _distanceThresholds = new()
        {
            { "ОКН", 200.0 },
            { "Территория ОКН", 100.0 },
            { "Охранная зона", 50.0 },
            { "Защитная зона", 20.0 },
            { "Здания", 20 }
        };
        
        public static List<Restriction> GetNearestRestrictions(Geometry geometry)
        {
            if (_restrictionsByType == null)
                _restrictionsByType = ReadData();

            var allRestrictions = new List<Restriction>();
            
            foreach (var kvp in _restrictionsByType)
            {
                var restrictionType = kvp.Key;
                var restrictions = kvp.Value;
                var distanceThreshold = _distanceThresholds[restrictionType];
                
                var nearbyRestrictions = restrictions
                    .Where(r => geometry.Distance(r.Geometry) < distanceThreshold)
                .ToList();
                
                allRestrictions.AddRange(nearbyRestrictions);
            }

            return allRestrictions;
        }

        private static Dictionary<string, List<Restriction>> ReadData()
        {
            var restrictionsByType = new Dictionary<string, List<Restriction>>();
            var fileMappings = new Dictionary<string, string>
            {
                { "okn_zona.json", "Охранная зона" },
                { "okn_granica.json", "Территория ОКН" },
                { "okn_zashit.json", "Защитная зона" },
                { "okn_place.json", "ОКН" },
                { "buildings.json", "Здания" }
            };

            foreach (var mapping in fileMappings)
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", mapping.Key);
                var jsonContent = File.ReadAllText(filePath);
                
                var fileRestrictions = ReadGeojsonWithPolygonsAndPoints(jsonContent, mapping.Value);
                
                // Initialize the list for this restricti on type if it doesn't exist
                if (!restrictionsByType.ContainsKey(mapping.Value))
                {
                    restrictionsByType[mapping.Value] = new List<Restriction>();
                }
                
                restrictionsByType[mapping.Value].AddRange(fileRestrictions);
            }
            
            return restrictionsByType;
        }

        public static List<Restriction> ReadGeojsonWithPolygonsAndPoints(string jsonContent, string restrictionType)
        {
            var restrictions = new List<Restriction>();
            var geoJson = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            var factory = GeometryFactory.Default;
            
            var features = geoJson.GetProperty("features");
            foreach (var feature in features.EnumerateArray())
            {
                var geometry = feature.GetProperty("geometry");
                var type = geometry.GetProperty("type");
                var coords = geometry.GetProperty("coordinates");
                
                var geomType = type.GetString();
                Geometry geometryObj = null;

                // Coordinate[] MakeRing(IEnumerable<Coordinate> a) => a.Append(a.First()).ToArray();
                
                if (geomType == "Polygon")
                {
                    var coordinates = coords.EnumerateArray().Select(ring => 
                        ring.EnumerateArray().Select(coord => 
                            new Coordinate(coord[1].GetDouble(), coord[0].GetDouble())).ToArray()).ToArray();
                    var rings = coordinates.ToArray();
                    
                    if (rings[0].Length < 3)
                        continue;
                    
                    var shell = factory.CreateLinearRing(rings[0].Append(rings[0][0]).ToArray());
                    var holes = rings.Skip(1).Select(r => factory.CreateLinearRing(r)).ToArray();
                    
                    geometryObj = factory.CreatePolygon(shell, holes);
                }
                else if (geomType == "Point")
                {
                    var coord = coords.EnumerateArray().ToArray();
                    geometryObj = new Point(new Coordinate(coord[1].GetDouble(), coord[0].GetDouble()));
                }
                
                if (geometryObj != null)
                {
                    if (geometryObj is Polygon polygon)
                    {
                        var (convertedPolygon, _) = CoordinatesConverter.ToUtm(polygon);
                        geometryObj = convertedPolygon;
                    }
                    else if (geometryObj is Point point)
                    {
                        var (convertedPoint, _) = CoordinatesConverter.ToUtm(point);
                        geometryObj = convertedPoint;
                    }
                    
                    // Create restriction name based on type and properties
                    string restrictionName = restrictionType;
                    if (restrictionType == "ОКН" && feature.TryGetProperty("properties", out var properties))
                    {
                        if (properties.TryGetProperty("hintContent", out var hintContent))
                        {
                            restrictionName = $"ОКН «{hintContent.GetString()}»";
                        }
                    }
                    
                    restrictions.Add(new Restriction { 
                        Geometry = geometryObj, 
                        RestrictionType = restrictionType,
                        Name = restrictionName
                    });
                }
            }
            
            return restrictions;
        }
    }
} 