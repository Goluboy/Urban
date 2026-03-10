using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Valid;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Urban.Domain.Common;
using Urban.Persistence.GeoJson.Extensions;

namespace Urban.Persistence.GeoJson.Services;

public class GeoJsonParser
{
    private readonly GeometryFactory _geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(4326);
    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        FloatParseHandling = FloatParseHandling.Double,
        DateParseHandling = DateParseHandling.None
    };

    /// <summary>
    /// Parses a GeoJSON FeatureCollection string into a list of domain entities.
    /// Handles both valid GeoJSON and common errors (e.g., unclosed rings).
    /// </summary>
    public List<GeoFeature> ParseGeoJson(string geoJson, string type)
    {
        if (string.IsNullOrWhiteSpace(geoJson))
            throw new ArgumentException("GeoJSON input cannot be null or empty", nameof(geoJson));

        try
        {
            // Primary parser: Strict GeoJSON parsing
            var reader = new GeoJsonReader(_geometryFactory, JsonSerializerSettings);
            var features = reader.Read<FeatureCollection>(geoJson);

            return features.Select(f => CreateGeoFeature(f, type)).ToList();
        }
        catch (JsonReaderException ex) when (IsUnclosedRingError(ex))
        {
            // Handle common topology errors (unclosed rings)
            return ParseWithRelaxedGeometry(geoJson, type);
        }
        catch (Exception ex) when (ex.Message.Contains("ring"))
        {
            return ParseWithRelaxedGeometry(geoJson, type);
        }
    }

    private GeoFeature CreateGeoFeature(IFeature feature, string type)
    {
        return new GeoFeature
        {
            GeometryType = Enum.Parse<Domain.Geometry.Data.GeometryType>(type),
            Geometry = feature.Geometry,
            Properties = feature.Attributes?.ToDictionary() ?? new Dictionary<string, object>()
        };
    }

    private bool IsUnclosedRingError(JsonReaderException ex)
    {
        // Common error patterns from NTS/GeoJSON.NET
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("ring") ||
               message.Contains("linear ring") ||
               message.Contains("close");
    }

    public async IAsyncEnumerable<GeoFeature> ParseFeaturesAsync(
    Stream stream,
    [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        using var streamReader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 81920,
            leaveOpen: false);

        using var reader = new JsonTextReader(streamReader)
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double
        };

        // 1. Validate root is FeatureCollection StartObject
        if (!await reader.ReadAsync(ct) || reader.TokenType != JsonToken.StartObject)
            throw new JsonException("Expected root object (FeatureCollection)");

        bool inFeaturesArray = false;
        int featuresArrayDepth = -1;

        while (await reader.ReadAsync(ct))
        {
            ct.ThrowIfCancellationRequested();

            switch (reader.TokenType)
            {
                case JsonToken.PropertyName:
                    var propName = reader.Value?.ToString();

                    // Detect "features" array at root level (depth 1)
                    if (inFeaturesArray == false &&
                        propName?.Equals("features", StringComparison.OrdinalIgnoreCase) == true &&
                        reader.Depth == 1)
                    {
                        // Next token must be StartArray
                        if (!await reader.ReadAsync(ct) || reader.TokenType != JsonToken.StartArray)
                            throw new JsonException("Expected 'features' array after property name");

                        inFeaturesArray = true;
                        featuresArrayDepth = reader.Depth; // Should be 1
                    }
                    break;

                case JsonToken.StartObject:
                    // Parse ONLY objects that are direct children of features array
                    if (inFeaturesArray && reader.Depth == featuresArrayDepth + 1)
                    {
                        var feature = await ParseFeatureAsync(reader, ct);
                        yield return feature;
                        // ⚠️ ParseFeatureAsync consumes the ENTIRE object (including its EndObject)
                        // Next ReadAsync() will be AFTER the feature object
                    }
                    break;

                case JsonToken.EndArray:
                    // Exit features array when we close the array at its original depth
                    if (inFeaturesArray && reader.Depth == featuresArrayDepth)
                    {
                        inFeaturesArray = false;
                    }
                    break;

                case JsonToken.EndObject:
                    // Root object closed (depth 0) - we're done
                    if (reader.Depth == 0)
                    {
                        yield break;
                    }
                    break;

                // Ignore other tokens (comments, primitives, etc.)
                case JsonToken.Comment:
                case JsonToken.None:
                case JsonToken.Undefined:
                case JsonToken.Integer:
                case JsonToken.Float:
                case JsonToken.String:
                case JsonToken.Boolean:
                case JsonToken.Null:
                    break;
            }
        }

        // If we exit loop naturally, JSON was fully consumed
        // JsonTextReader would have thrown if structure was invalid
    }

    private async Task<GeoFeature> ParseFeatureAsync(
        JsonTextReader reader,
        CancellationToken ct)
    {
        var properties = new Dictionary<string, object>();
        Geometry? geometry = null;
        string? currentProperty = null;
        bool inProperties = false;
        bool inGeometry = false;
        int objectDepth = reader.Depth;

        while (await reader.ReadAsync(ct) && reader.Depth > objectDepth)
        {
            ct.ThrowIfCancellationRequested();

            switch (reader.TokenType)
            {
                case JsonToken.PropertyName:
                    var propName = reader.Value?.ToString();

                    if (propName?.Equals("geometry", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        inGeometry = true;
                        inProperties = false;
                        currentProperty = null;
                    }
                    else if (propName?.Equals("properties", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        inProperties = true;
                        inGeometry = false;
                        currentProperty = null;

                        // Skip null properties
                        if (!await reader.ReadAsync(ct) || reader.TokenType == JsonToken.Null)
                            continue;

                        // Must be object
                        if (reader.TokenType != JsonToken.StartObject)
                            throw new JsonException("Expected properties object");
                    }
                    else if (inProperties && reader.Depth == objectDepth + 2)
                    {
                        currentProperty = propName;
                    }
                    break;

                case JsonToken.StartObject:
                    if (inGeometry && reader.Depth == objectDepth + 2)
                    {
                        // Parse geometry object
                        geometry = await ParseGeometryAsync(reader, ct);
                        inGeometry = false;
                    }
                    break;

                case JsonToken.String:
                case JsonToken.Integer:
                case JsonToken.Float:
                case JsonToken.Boolean:
                case JsonToken.Null:
                    if (inProperties && currentProperty != null && reader.Depth == objectDepth + 3)
                    {
                        if (reader.Value != null) 
                            properties[currentProperty] = reader.Value;
                        currentProperty = null;
                    }
                    break;

                case JsonToken.EndObject:
                    if (reader.Depth == objectDepth)
                        return new GeoFeature
                        {
                            Geometry = geometry ?? _geometryFactory.CreatePoint(new Coordinate(0, 0)),
                            Properties = properties
                        };
                    break;
            }
        }

        return new GeoFeature
        {
            Geometry = geometry ?? _geometryFactory.CreatePoint(new Coordinate(0, 0)),
            Properties = properties
        };
    }

    private async Task<Geometry?> ParseGeometryAsync(JsonTextReader reader, CancellationToken ct)
    {
        // Use GeoJsonReader for actual geometry parsing (handles all geometry types + ring repair)
        var geoJsonReader = new GeoJsonReader(_geometryFactory, JsonSerializerSettings);

        // Capture geometry JSON fragment
        var geometryJson = await CaptureJsonFragmentAsync(reader, ct);

        try
        {
            return geoJsonReader.Read<Geometry>(geometryJson);
        }
        catch
        {
            // Repair unclosed rings and retry
            var repairedJson = RepairUnclosedRings(geometryJson);
            return geoJsonReader.Read<Geometry>(repairedJson);
        }
    }
    private string RepairUnclosedRings(string geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson))
            return geoJson;

        try
        {
            // Parse entire GeoJSON structure
            var root = JObject.Parse(geoJson);
            RepairGeometryRecursively(root);
            return root.ToString(Formatting.None);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Invalid GeoJSON format", nameof(geoJson), ex);
        }
    }

    private void RepairGeometryRecursively(JToken token)
    {
        if (token is JObject obj)
        {
            // Check if this is a Geometry object
            if (obj.TryGetValue("type", out var typeToken) &&
                typeToken.Type == JTokenType.String)
            {
                var type = typeToken.ToString();

                if (type.Equals("Polygon", StringComparison.OrdinalIgnoreCase) ||
                    type.Equals("MultiPolygon", StringComparison.OrdinalIgnoreCase))
                {
                    GeoJsonRingRepair.RepairPolygonGeometry(obj);
                }
            }

            // Recurse into all properties
            foreach (var property in obj.Properties().ToList())
            {
                RepairGeometryRecursively(property.Value);
            }
        }
        else if (token is JArray array)
        {
            foreach (var item in array)
            {
                RepairGeometryRecursively(item);
            }
        }
    }

    private async Task<string> CaptureJsonFragmentAsync(JsonTextReader reader, CancellationToken ct)
    {
        var writer = new StringWriter();
        var jsonWriter = new JsonTextWriter(writer);

        int depth = reader.Depth;
        bool first = true;

        do
        {
            if (!first && reader.Depth <= depth) break;

            switch (reader.TokenType)
            {
                case JsonToken.StartObject:
                case JsonToken.StartArray:
                    await jsonWriter.WriteStartArrayAsync(ct);
                    break;

                case JsonToken.EndObject:
                case JsonToken.EndArray:
                    await jsonWriter.WriteEndArrayAsync(ct);
                    break;

                case JsonToken.PropertyName:
                    await jsonWriter.WritePropertyNameAsync(reader.Value?.ToString() ?? string.Empty, ct);
                    break;

                case JsonToken.String:
                    await jsonWriter.WriteValueAsync(reader.Value?.ToString(), ct);
                    break;

                case JsonToken.Integer:
                    await jsonWriter.WriteValueAsync(Convert.ToInt64(reader.Value), ct);
                    break;

                case JsonToken.Float:
                    await jsonWriter.WriteValueAsync(Convert.ToDouble(reader.Value), ct);
                    break;

                case JsonToken.Boolean:
                    await jsonWriter.WriteValueAsync(Convert.ToBoolean(reader.Value), ct);
                    break;

                case JsonToken.Null:
                    await jsonWriter.WriteNullAsync(ct);
                    break;
            }

            first = false;
        }
        while (await reader.ReadAsync(ct) && reader.Depth >= depth);

        await jsonWriter.FlushAsync(ct);
        return writer.ToString();
    }

    private List<GeoFeature> ParseWithRelaxedGeometry(string geoJson, string type)
    {
        // Strategy: Preprocess GeoJSON to close rings before parsing
        try
        {
            var settings = new JsonSerializerSettings
            {
                FloatParseHandling = FloatParseHandling.Double,
                DateParseHandling = DateParseHandling.None
            };

            var parsedJson = JsonConvert.DeserializeObject<JObject>(geoJson, settings);
            CloseRingsInCoordinates(parsedJson);

            var fixedGeoJson = JsonConvert.SerializeObject(parsedJson, Formatting.None);
            var reader = new GeoJsonReader(_geometryFactory, JsonSerializerSettings);
            var features = reader.Read<FeatureCollection>(fixedGeoJson);

            return features.Select(f => CreateGeoFeature(f, type)).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse GeoJSON after ring-closure fallback. Original error: {ex.Message}",
                ex);
        }
    }

    private void CloseRingsInCoordinates(JToken token)
    {
        if (token is JArray { Count: > 0 } array)
        {
            // Check if this is a coordinate array (numbers)
            if (array[0].Type == JTokenType.Float || array[0].Type == JTokenType.Integer)
            {
                // Skip - this is a single coordinate [x,y]
                return;
            }

            // Recursively process nested arrays
            foreach (var item in array.Children())
            {
                CloseRingsInCoordinates(item);
            }

            // If this array represents a linear ring (first coord == last coord required)
            // Heuristic: Array of arrays where inner arrays are coordinates
            if (array.Count >= 4 &&
                array.All(a => a is JArray jArray &&
                               jArray.Count >= 2 &&
                               jArray[0].Type == JTokenType.Float &&
                               jArray[1].Type == JTokenType.Float))
            {
                var firstCoordinate = (JArray)array[0];
                var lastCoordinate = (JArray)array[^1]!;

                // Close the ring if not already closed
                if (lastCoordinate != null && !CoordinatesEqual(firstCoordinate, lastCoordinate))
                {
                    array.Add(firstCoordinate.DeepClone());
                }
            }
        }
        else if (token is JObject obj)
        {
            // Process geometry objects
            if (obj.TryGetValue("coordinates", out var coords))
            {
                CloseRingsInCoordinates(coords);
            }

            // Recurse into all properties
            foreach (var property in obj.Properties().ToList())
            {
                CloseRingsInCoordinates(property.Value);
            }
        }
    }

    private bool CoordinatesEqual(JArray a, JArray b)
    {
        if (a.Count != b.Count) 
            return false;

        return !a.Where((t, i) => !JToken.DeepEquals(t, b[i])).Any();
    }
}