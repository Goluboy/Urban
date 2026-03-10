using NetTopologySuite;
using NetTopologySuite.Geometries;
using Newtonsoft.Json.Linq;


namespace Urban.Persistence.GeoJson.Services;

public static class GeoJsonRingRepair
{
    private const double CoordinateTolerance = 1e-9;

    /// <summary>
    /// Repairs unclosed rings in GeoJSON Polygon/MultiPolygon geometries.
    /// Uses manual ring closure with floating-point tolerance (no NTS dependency required).
    /// </summary>
    public static void RepairPolygonGeometry(JObject geometry)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));

        // Validate geometry type
        if (!geometry.TryGetValue("type", out var typeToken) ||
            !(typeToken is JValue typeValue) ||
            !(typeValue.Value is string typeStr))
        {
            throw new ArgumentException("Invalid geometry: missing 'type' property", nameof(geometry));
        }

        var type = typeStr.Trim();
        if (type.Equals("Polygon", StringComparison.OrdinalIgnoreCase))
        {
            RepairPolygonRings(geometry);
        }
        else if (type.Equals("MultiPolygon", StringComparison.OrdinalIgnoreCase))
        {
            RepairMultiPolygonRings(geometry);
        }
        // Other geometry types don't require ring repair
    }

    private static void RepairPolygonRings(JObject geometry)
    {
        if (!geometry.TryGetValue("coordinates", out var coordsToken) ||
            coordsToken.Type != JTokenType.Array)
        {
            throw new ArgumentException("Invalid Polygon: missing 'coordinates' array");
        }

        var rings = (JArray)coordsToken;
        foreach (var ring in rings)
        {
            if (ring is JArray ringArray && ringArray.Count >= 2)
            {
                CloseLinearRing(ringArray);
            }
        }
    }

    private static void RepairMultiPolygonRings(JObject geometry)
    {
        if (!geometry.TryGetValue("coordinates", out var coordsToken) ||
            coordsToken.Type != JTokenType.Array)
        {
            throw new ArgumentException("Invalid MultiPolygon: missing 'coordinates' array");
        }

        var polygons = (JArray)coordsToken;
        foreach (var polygon in polygons)
        {
            if (polygon is JArray polygonArray)
            {
                foreach (var ring in polygonArray)
                {
                    if (ring is JArray ringArray && ringArray.Count >= 2)
                    {
                        CloseLinearRing(ringArray);
                    }
                }
            }
        }
    }

    private static void CloseLinearRing(JArray ring)
    {
        if (ring.Count < 2)
            return; // Invalid ring

        // Get first and last coordinates (handle 2D/3D)
        var firstCoord = GetCoordinateArray(ring.First);
        var lastCoord = GetCoordinateArray(ring.Last);

        // Check if ring is already closed (within tolerance)
        if (IsCoordinateEqual(firstCoord, lastCoord))
            return;

        // Close the ring by duplicating first coordinate
        ring.Add(JArray.FromObject(firstCoord));
    }

    private static double[] GetCoordinateArray(JToken token)
    {
        if (token is JArray array && array.Count >= 2)
        {
            var coords = new double[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                coords[i] = array[i].Value<double>();
            }
            return coords;
        }
        throw new ArgumentException($"Invalid coordinate: {token}");
    }

    private static bool IsCoordinateEqual(double[] a, double[] b)
    {
        if (a.Length < 2 || b.Length < 2)
            return false;

        // Compare with tolerance for floating-point precision
        return Math.Abs(a[0] - b[0]) < CoordinateTolerance &&
               Math.Abs(a[1] - b[1]) < CoordinateTolerance &&
               (a.Length < 3 || b.Length < 3 || Math.Abs(a[2] - b[2]) < CoordinateTolerance);
    }
}