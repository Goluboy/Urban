using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Urban.Application.Interfaces;
using Urban.Domain.Common;
using Urban.Domain.Geometry.Data;

namespace Urban.Persistence.GeoJson;

/// <summary>
/// Repository used for bulk operations with data using Npgsql
/// </summary>
/// <param name="connectionString"></param>
public class GeoFeatureRepository(string? connectionString, ApplicationDbContext context) : IGeoFeatureRepository
{
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
        var jsonOptions = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

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

    public async Task<List<Restriction>> GetNearestRestrictions(Geometry geometry, RestrictionType restrictionType, double distanceThreshold, CancellationToken ct = default)
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

        var query = context.Restrictions.FromSqlRaw(sql, typeParam, geomParam, sridParam, distanceParam).AsNoTracking();

        var list = await query.ToListAsync(ct);

        foreach (var r in list)
            r.BoundingBox = r.Geometry?.EnvelopeInternal;

        return list;
    }

    public async Task<int> BulkInsertAsync(string geoJson, string type, CancellationToken ct)
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

    public async Task ImportFromFileAsync(IFormFile file, string type, CancellationToken ct = default)
    {
        // Use Stream to avoid loading entire file into memory
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);

        var jsonString = await reader.ReadToEndAsync(ct);

        await BulkInsertAsync(jsonString, type, ct);
    }
}