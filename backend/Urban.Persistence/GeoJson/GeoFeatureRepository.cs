using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Urban.Application.Interfaces;
using Urban.Application.Interfaces.Results;
using Urban.Domain.Common;
using Urban.Domain.Geometry.Data;
using Urban.Persistence.GeoJson.Services;

namespace Urban.Persistence.GeoJson;


public class GeoFeatureRepository(string? connectionString, ApplicationDbContext context, GeoJsonParser geoJsonParser) : IGeoFeatureRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<List<Restriction>> GetRestrictionsByType(string type, CancellationToken ct = default)
    {
        var results = new List<Restriction>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT
                "Id",
                ST_AsBinary("Geometry") AS geom_wkb,
                ST_SRID("Geometry") AS srid,
                "Properties"::text AS properties,
                "Discriminator",
                "DateCreated",
                "DateUpdated",
                "DateDeleted",
                "UserId",
                "IsDeleted"
            FROM "Restrictions"
            WHERE "Discriminator" = @type
              AND ("IsDeleted" IS NULL OR "IsDeleted" = FALSE);
        """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("type", type);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var wkbReader = new WKBReader();

        while (await reader.ReadAsync(ct))
        {
            var gf = new Restriction();

            // Id
            if (!reader.IsDBNull(0))
                gf.Id = reader.GetFieldValue<Guid>(0);

            // Geometry
            if (!reader.IsDBNull(1))
            {
                var bytes = reader.GetFieldValue<byte[]>(1);
                try
                {
                    var geom = wkbReader.Read(bytes);
                    if (!reader.IsDBNull(2))
                    {
                        geom.SRID = reader.GetFieldValue<int>(2);
                    }
                    gf.Geometry = geom;
                }
                catch
                {
                    // ignore malformed geometry for this row
                    gf.Geometry = null;
                }
            }

            // Properties (jsonb as text)
            if (!reader.IsDBNull(3))
            {
                var propsText = reader.GetFieldValue<string>(3);
                try
                {
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(propsText, JsonOptions);
                    gf.Properties = dict;
                }
                catch
                {
                    gf.Properties = null;
                }
            }

            // Discriminator
            if (!reader.IsDBNull(4))
            {
                var discriminatorText = reader.GetFieldValue<string>(4)!;
                gf.Discriminator = Enum.Parse<RestrictionType>(discriminatorText, ignoreCase: true);
            }

            // Dates and other metadata
            if (!reader.IsDBNull(5))
                gf.DateCreated = reader.GetFieldValue<DateTimeOffset>(5);

            if (!reader.IsDBNull(6))
                gf.DateUpdated = reader.GetFieldValue<DateTimeOffset?>(6);

            if (!reader.IsDBNull(7))
                gf.DateDeleted = reader.GetFieldValue<DateTimeOffset?>(7);

            if (!reader.IsDBNull(8))
                gf.UserId = reader.GetFieldValue<Guid>(8);

            if (!reader.IsDBNull(9))
                gf.IsDeleted = reader.GetFieldValue<bool>(9);

            results.Add(gf);
        }

        return results;
    }

    
    public async Task<List<Restriction>> GetRestrictionsByType(RestrictionType type, CancellationToken ct = default)
    {
        return await GetRestrictionsByType(type.ToString(), ct);
    }
    
    public async Task<List<Restriction>> GetNearestRestrictions(
        Geometry geometry, 
        RestrictionType restrictionType, 
        double distanceThreshold, 
        CancellationToken ct = default)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));


        // Prepare WKB for parameter
        var wkbWriter = new WKBWriter();
        var geomBytes = wkbWriter.Write(geometry);

        // Raw SQL using geography for meter-accurate distances
        const string sql = """
                           SELECT * FROM "Restrictions" r 
                           WHERE r."Discriminator" = @type
                               AND(r."IsDeleted" IS NULL OR r."IsDeleted" = FALSE)
                               AND ST_DWithin(r."Geometry"::geography, ST_SetSRID(ST_GeomFromWKB(@geom), @srid)::geography, @distance)
                           ORDER BY ST_Distance(r."Geometry"::geography, ST_SetSRID(ST_GeomFromWKB(@geom), @srid)::geography) ASC;
                           """;


        var geomParam = new NpgsqlParameter("geom", NpgsqlDbType.Bytea) { Value = geomBytes };
        var sridParam = new NpgsqlParameter("srid", 4326);
        var typeParam = new NpgsqlParameter("type", restrictionType.ToString());
        var distanceParam = new NpgsqlParameter("distance", distanceThreshold);

        var query = context.Restrictions.FromSqlRaw(sql, typeParam, geomParam, sridParam, distanceParam)
            .AsNoTracking();

        var list = await query.ToListAsync(ct);

        foreach (var r in list)
            r.BoundingBox = r.Geometry?.EnvelopeInternal;

        return list;
    }

    
    public async Task<int> BulkInsertEntireJsonAsync(string geoJson, string type, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(geoJson))
            return 0;

        await using var conn = new NpgsqlConnection(connectionString);

        await conn.OpenAsync(ct);

        // Ensure all non-nullable columns are provided to avoid NOT NULL constraint violations
        const string sql = """
            WITH data AS (
                 SELECT @geoJson::jsonb AS fc
             ),
             parsed_features AS (
                 SELECT
                     gen_random_uuid() AS id,
                     ST_GeomFromGeoJSON(feat->>'geometry') AS geom,
                     feat - 'geometry' AS attributes
                 FROM (
                     SELECT jsonb_array_elements(fc->'features') AS feat
                     FROM data
                 ) AS f
             )
             INSERT INTO "Restrictions" ("Id", "Geometry", "Properties", "Discriminator", "DateCreated", "DateUpdated", "DateDeleted", "UserId", "IsDeleted")
             SELECT 
                 id,
                 geom::geometry(Geometry, 4326),
                 attributes::jsonb,
                 @type,
                 NOW() AT TIME ZONE 'UTC', -- DateCreated (set current UTC time)
                 NULL,             -- DateUpdated
                 NULL,             -- DateDeleted
                 gen_random_uuid(),-- UserId (placeholder generated UUID)
                 false             -- IsDeleted
             FROM parsed_features
             RETURNING "Id";
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("geoJson", geoJson);
        cmd.Parameters.AddWithValue("type", type);

        var affectedNumber = await cmd.ExecuteNonQueryAsync(ct);

        return affectedNumber;
    }

    public async Task<ImportResult> ImportGeoJsonStreamAsync(Stream stream, string type, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var featureCount = 0;
        const int batchSize = 1000;
        var batch = new List<GeoFeature>(batchSize);

        // Use Utf8JsonReader for true streaming (no full-document load)
        using var jsonReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 81920);
        using var reader = new JsonTextReader(jsonReader) { SupportMultipleContent = false };

        // Parse GeoJSON features array incrementally
        await foreach (var feature in geoJsonParser.ParseFeaturesAsync(stream, ct))
        {
            ct.ThrowIfCancellationRequested();
            batch.Add(feature);
            featureCount++;

            // Batch insert every 1,000 features
            if (batch.Count < batchSize) continue;

            await BulkInsertFeaturesAsync(batch, type, ct);
            batch.Clear();
        }

        // Insert final batch
        if (batch.Count > 0)
            await BulkInsertFeaturesAsync(batch, type, ct);

        stopwatch.Stop();
        return new ImportResult { FeatureCount = featureCount, ElapsedMilliseconds = stopwatch.ElapsedMilliseconds };
    }

    /// <summary>
    /// Bulk inserts GeoFeatures using Npgsql binary COPY (fastest method)
    /// </summary>
    public async Task BulkInsertFeaturesAsync(
        List<GeoFeature> features,
        string type,
        CancellationToken ct = default)
    {
        if (features == null || features.Count == 0)
            return;
        
        // Ensure all geometries have SRID 4326 before insertion
        foreach (var feature in features)
        {
            if (feature.Geometry != null && feature.Geometry.SRID == 0)
                feature.Geometry.SRID = 4326;
        }

        // PostGisWriter to convert NetTopologySuite geometries to WKB for binary COPY
        var postGisWriter = new PostGisWriter
        {
            HandleOrdinates = Ordinates.XY
        };

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // Use binary COPY (avoids sql parsing overhead)
        await using var writer = await conn.BeginBinaryImportAsync(
            @"COPY ""Restrictions"" 
              (""Id"", ""Geometry"", ""Properties"", ""Discriminator"", 
               ""DateCreated"", ""DateUpdated"", ""DateDeleted"", ""UserId"", ""IsDeleted"")
              FROM STDIN (FORMAT BINARY)",
            ct);

        var nowUtc = DateTimeOffset.UtcNow;

        foreach (var feature in features)
        {
            ct.ThrowIfCancellationRequested();

            await writer.StartRowAsync(ct);

            // 1. Id (Guid)
            await writer.WriteAsync(feature.Id, NpgsqlDbType.Uuid, ct);

            // 2. Geometry 
            if (feature.Geometry != null)
            {
                // Critical: Ensure SRID is set before writing
                if (feature.Geometry.SRID == 0)
                    feature.Geometry.SRID = 4326;

                // Write converted geometry
                await writer.WriteAsync(postGisWriter.Write(feature.Geometry), ct);
            }
            else
            {
                await writer.WriteNullAsync(ct);
            }

            // 3. Properties jsonb
            if (feature.Properties != null)
            {
                var jsonBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                    feature.Properties,
                    JsonOptions);

                await writer.WriteAsync(
                    jsonBytes,
                    NpgsqlDbType.Jsonb,
                    ct);
            }
            else
            {
                await writer.WriteNullAsync(ct);
            }

            // 4. Discriminator (string enum value)
            await writer.WriteAsync(type, NpgsqlDbType.Text, ct);

            // 5. DateCreated (UTC now)
            await writer.WriteAsync(nowUtc, NpgsqlDbType.TimestampTz, ct);

            // 6. DateUpdated (NULL)
            await writer.WriteNullAsync(ct);

            // 7. DateDeleted (NULL)
            await writer.WriteNullAsync(ct);

            // 8. UserId placeholder
            await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid, ct);

            // 9. IsDeleted (false)
            await writer.WriteAsync(false, NpgsqlDbType.Boolean, ct);
        }

        // Commit the COPY operation
        var rowsInserted = await writer.CompleteAsync(ct);

        if (rowsInserted != (ulong)features.Count)
        {
            throw new InvalidOperationException(
                $"COPY operation inserted {rowsInserted} rows but expected {features.Count}");
        }
    }
}