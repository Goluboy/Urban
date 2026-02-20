using Microsoft.AspNetCore.Http;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;
using System.Text.Json.Serialization;
using Urban.Application.Interfaces;
using Urban.Domain.Common;
using Urban.Domain.Geometry;
using Urban.Persistence.GeoJson.Services;

namespace Urban.Persistence.GeoJson;

/// <summary>
/// Repository used for bulk operations with data using Npgsql Binary
/// </summary>
/// <param name="connectionString"></param>
public class GeoFeatureRepository(string? connectionString, ApplicationDbContext context) : IGeoFeatureRepository
{
    public async Task<List<GeoFeature>> GetGeoFeaturesByType(string type, CancellationToken ct = default)
    {
        var results = new List<GeoFeature>();

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
            FROM "GeoFeatures"
            WHERE "Discriminator" = @type
              AND ("IsDeleted" IS NULL OR "IsDeleted" = FALSE);
        """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("type", type ?? string.Empty);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var wkbReader = new WKBReader();
        var jsonOptions = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

        while (await reader.ReadAsync(ct))
        {
            var gf = new GeoFeature();

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
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(propsText, jsonOptions);
                    gf.Properties = dict;
                }
                catch
                {
                    gf.Properties = null;
                }
            }

            // Discriminator
            if (!reader.IsDBNull(4))
                gf.Discriminator = reader.GetFieldValue<string>(4)!;

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
    
    public async Task<IList<Restriction>> GetNearestRestrictions(Geometry geometry, string restrictionType, double distanceThreshold, CancellationToken ct = default)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));

        var results = new List<Restriction>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT
                ST_AsBinary("Geometry") AS geom_wkb,
                ST_SRID("Geometry") AS srid,
                "Properties"::text AS properties,
                COALESCE(("Properties"->>'name'), ("Properties"->>'hintContent'), '') AS name,
                "Discriminator"
            FROM "GeoFeatures"
            WHERE "Discriminator" = @type
              AND ("IsDeleted" IS NULL OR "IsDeleted" = FALSE)
              AND ST_Distance("Geometry", ST_SetSRID(ST_GeomFromWKB(@geom), @srid)) < @distance
            ORDER BY ST_Distance("Geometry", ST_SetSRID(ST_GeomFromWKB(@geom), @srid)) ASC
            LIMIT 100;
        """;

        await using var cmd = new NpgsqlCommand(sql, conn);

        var wkbWriter = new WKBWriter();
        var geomBytes = wkbWriter.Write(geometry);

        var geomParam = new NpgsqlParameter("geom", NpgsqlDbType.Bytea) { Value = geomBytes };
        cmd.Parameters.Add(geomParam);
        cmd.Parameters.AddWithValue("srid", geometry.SRID);
        cmd.Parameters.AddWithValue("type", restrictionType ?? string.Empty);
        cmd.Parameters.AddWithValue("distance", distanceThreshold);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var wkbReader = new WKBReader();
        var jsonOptions = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

        while (await reader.ReadAsync(ct))
        {
            var r = new Restriction();

            // Geometry
            if (!reader.IsDBNull(0))
            {
                var bytes = reader.GetFieldValue<byte[]>(0);
                try
                {
                    var geom = wkbReader.Read(bytes);
                    if (!reader.IsDBNull(1))
                        geom.SRID = reader.GetFieldValue<int>(1);
                    r.Geometry = geom;
                }
                catch
                {
                    r.Geometry = null;
                }
            }

            // Properties and name
            string name = string.Empty;
            if (!reader.IsDBNull(2))
            {
                var propsText = reader.GetFieldValue<string>(2);
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(propsText, jsonOptions);
                    // try common name keys
                    if (dict != null)
                    {
                        if (dict.TryGetValue("name", out var v) || dict.TryGetValue("Name", out v) || dict.TryGetValue("hintContent", out v))
                            name = v?.ToString() ?? string.Empty;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (string.IsNullOrEmpty(name) && !reader.IsDBNull(3))
                name = reader.GetFieldValue<string>(3) ?? string.Empty;

            r.Name = name;

            if (!reader.IsDBNull(4))
                r.RestrictionType = reader.GetFieldValue<string>(4)!;

            results.Add(r);
        }

        return results;
    }

    public async Task<int> BulkInsertAsync(string geoJson, string type, CancellationToken ct)
    {
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
               INSERT INTO "GeoFeatures" ("Id", "Geometry", "Properties", "Discriminator", "DateCreated", "DateUpdated", "DateDeleted", "UserId", "IsDeleted")
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

    public async Task ImportFromFileAsync(IFormFile file, string type, CancellationToken ct = default)
    {
        // Use Stream to avoid loading entire file into memory
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);

        var jsonString = await reader.ReadToEndAsync(ct);
        var features = GeoJsonParser.ParseGeoJson(jsonString);

        await BulkInsertAsync(jsonString, type, ct);
    }

    public Task<bool> EmptyGeoTable(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}