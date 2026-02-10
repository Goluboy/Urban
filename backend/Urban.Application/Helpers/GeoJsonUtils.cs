using System.Text.Json;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;

namespace Urban.Application.Helpers
{
    public static class GeoJsonUtils
    {
        /// <summary>
        /// Converts a JsonElement containing GeoJSON to a Polygon geometry
        /// </summary>
        /// <param name="geoJsonElement">The JsonElement containing GeoJSON data</param>
        /// <returns>A Polygon geometry or null if conversion fails</returns>
        public static Polygon ConvertToPolygon(JsonElement geoJsonElement)
        {
            // Check if the JsonElement contains a string that needs to be parsed
            JsonElement actualGeoJson;
            if (geoJsonElement.ValueKind == JsonValueKind.String)
            {
                var jsonString = geoJsonElement.GetString();
                actualGeoJson = JsonDocument.Parse(jsonString).RootElement;
            }
            else
            {
                actualGeoJson = geoJsonElement;
            }
            
            var type = actualGeoJson.GetProperty("type").GetString();
            if (type != "Polygon")
            {
                throw new ArgumentException($"Expected Polygon geometry type, but got: {type}");
            }

            var coords = actualGeoJson.GetProperty("coordinates");
            var factory = GeometryFactory.Default;
            
            var coordinates = coords.EnumerateArray().Select(ring => 
                ring.EnumerateArray().Select(coord => 
                    new Coordinate(coord[0].GetDouble(), coord[1].GetDouble())).ToArray()).ToArray();
            
            var rings = coordinates.ToArray();
            
            if (rings[0].Length < 3)
            {
                throw new ArgumentException($"Invalid polygon - need at least 3 points, but got {rings[0].Length}");
            }
            
            // Ensure the polygon is closed by checking if first and last points are the same
            var firstPoint = rings[0][0];
            var lastPoint = rings[0][rings[0].Length - 1];
            
            if (firstPoint.X != lastPoint.X || firstPoint.Y != lastPoint.Y)
            {
                // Close the polygon by adding the first point at the end
                var closedRing = rings[0].Append(firstPoint).ToArray();
                var shell = factory.CreateLinearRing(closedRing);
                var holes = rings.Skip(1).Select(r => factory.CreateLinearRing(r)).ToArray();
                return factory.CreatePolygon(shell, holes);
            }
            else
            {
                // Polygon is already closed
                var shell = factory.CreateLinearRing(rings[0]);
                var holes = rings.Skip(1).Select(r => factory.CreateLinearRing(r)).ToArray();
                return factory.CreatePolygon(shell, holes);
            }
        }

        /// <summary>
        /// Converts double[][][] coordinates to a Polygon geometry
        /// </summary>
        /// <param name="coordinates">The coordinates array in format double[][][]</param>
        /// <returns>A Polygon geometry or null if conversion fails</returns>
        public static Polygon ConvertFromCoordinates(double[][][] coordinates)
        {
            if (coordinates == null || coordinates.Length == 0)
            {
                throw new ArgumentException("Coordinates array cannot be null or empty");
            }

            var factory = GeometryFactory.Default;
            
            // Convert the first ring (exterior boundary)
            var exteriorRing = coordinates[0].Select(coord => new Coordinate(coord[0], coord[1])).ToArray();
            
            if (exteriorRing.Length < 3)
            {
                throw new ArgumentException($"Invalid polygon - need at least 3 points, but got {exteriorRing.Length}");
            }
            
            // Ensure the polygon is closed by checking if first and last points are the same
            var firstPoint = exteriorRing[0];
            var lastPoint = exteriorRing[exteriorRing.Length - 1];
            
            if (firstPoint.X != lastPoint.X || firstPoint.Y != lastPoint.Y)
            {
                // Close the polygon by adding the first point at the end
                var closedRing = exteriorRing.Append(firstPoint).ToArray();
                var shell = factory.CreateLinearRing(closedRing);
                
                // Handle interior rings (holes) if any
                var holes = coordinates.Skip(1).Select(ring => 
                    factory.CreateLinearRing(ring.Select(coord => new Coordinate(coord[0], coord[1])).ToArray())).ToArray();
                
                return factory.CreatePolygon(shell, holes);
            }
            else
            {
                // Polygon is already closed
                var shell = factory.CreateLinearRing(exteriorRing);
                
                // Handle interior rings (holes) if any
                var holes = coordinates.Skip(1).Select(ring => 
                    factory.CreateLinearRing(ring.Select(coord => new Coordinate(coord[0], coord[1])).ToArray())).ToArray();
                
                return factory.CreatePolygon(shell, holes);
            }
        }

        /// <summary>
        /// Converts a geometry to GeoJSON coordinates format for display on map
        /// Assumes the geometry is already in WGS84 coordinate system
        /// </summary>
        /// <param name="geometry">The geometry to convert (should be in WGS84)</param>
        /// <returns>GeoJSON coordinates array or null if conversion fails</returns>
        public static object ConvertGeometryToCoordinates(Geometry geometry)
        {
            if (geometry is Polygon polygon)
            {
                return new[] { polygon.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray() };
            }
            else if (geometry is MultiPolygon multiPolygon)
            {
                return multiPolygon.Geometries.OfType<Polygon>()
                    .Select(p => p.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray())
                    .ToArray();
            }
            else if (geometry is NetTopologySuite.Geometries.Point point)
            {
                return new[] { point.X, point.Y };
            }
            else if (geometry is LineString lineString)
            {
                return lineString.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray();
            }
            
            return null;
        }

        /// <summary>
        /// Extension method to convert a Polygon to double[][][] format
        /// </summary>
        /// <param name="polygon">The polygon to convert</param>
        /// <returns>double[][][] array representing the polygon coordinates</returns>
        public static double[][][] ToDoubleArray(this Polygon polygon)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            var result = new List<double[][]>();
            
            // Add exterior ring
            var exteriorRing = polygon.ExteriorRing.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray();
            result.Add(exteriorRing);
            
            // Add interior rings (holes) if any
            foreach (var hole in polygon.Holes)
            {
                var holeRing = hole.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray();
                result.Add(holeRing);
            }
            
            return result.ToArray();
        }

        /// <summary>
        /// Extension method to convert a Polygon to double[][][] format in WGS84 coordinates
        /// </summary>
        /// <param name="polygon">The polygon to convert (assumed to be in UTM coordinates)</param>
        /// <param name="utmSystem">The UTM coordinate system used for the polygon coordinates</param>
        /// <returns>double[][][] array representing the polygon coordinates in WGS84</returns>
        public static double[][][] ToDoubleArrayWgs84(this Polygon polygon, ProjectedCoordinateSystem utmSystem)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            // Convert to WGS84 first using the provided coordinate system
            var wgs84Polygon = CoordinatesConverter.FromUtm(polygon, utmSystem);
            return ((Polygon)wgs84Polygon).ToDoubleArray();
        }
    }
} 