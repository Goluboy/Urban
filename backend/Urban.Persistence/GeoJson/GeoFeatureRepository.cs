using Microsoft.AspNetCore.Http;
using Npgsql;
using Urban.Application.Services;
using Urban.Domain.Geometry;
using Urban.Persistence.GeoJson.Interfaces;
using Urban.Persistence.GeoJson.Services;

namespace Urban.Persistence.GeoJson;

/// <summary>
/// Repository used for bulk insertion. Takes geoJson as a string and store to db using Npgsql Binary Copy
/// </summary>
/// <param name="connectionString"></param>
public class GeoFeatureRepository(string? connectionString) : IGeoFeatureRepository
{
    public async Task<Restriction> GetNearestRestrictions(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);

        await conn.OpenAsync(ct);


    }

    public async Task<int> BulkInsertAsync(string geoJson, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);

        await conn.OpenAsync(ct);
        
        const string sql = """
            WITH data AS (
                   SELECT @geoJson::jsonb AS fc
               ),
               parsed_features AS (
                   SELECT
                       gen_random_uuid() AS id,
                       ST_GeomFromGeoJSON(feat->>'geometry') AS geom,
                       feat->'properties' AS properties
                   FROM (
                       SELECT jsonb_array_elements(fc->'features') AS feat
                       FROM data
                   ) AS f
               )
               INSERT INTO "GeoFeatures" ("Id", "Geometry", "Properties")
               SELECT 
                   id,
                   geom::geometry(Geometry, 4326),
                   properties::jsonb
               FROM parsed_features
               RETURNING "Id";
         """;
        
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("geoJson", geoJson);

        var affectedNumber = await cmd.ExecuteNonQueryAsync(ct);
        

        return affectedNumber;
    }

    public async Task ImportFromFileAsync(IFormFile file, CancellationToken ct = default)
    {
        // Use Stream to avoid loading entire file into memory
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);

        var jsonString = await reader.ReadToEndAsync(ct);
        var features = GeoJsonParser.ParseGeoJson(jsonString);

        await BulkInsertAsync(jsonString, ct);
    }
}