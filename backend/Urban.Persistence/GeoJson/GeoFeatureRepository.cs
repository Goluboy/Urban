using Npgsql;
using NpgsqlTypes;
using Urban.Domain.Common;

namespace Urban.Persistence.GeoJson;

/// <summary>
/// Repository used for bulk insertion. Takes GeoFeatures and store to db using Npgsql Binary Copy
/// </summary>
/// <param name="connectionString"></param>
public class GeoFeatureRepository(string? connectionString)
{
    public async Task BulkInsertAsync(List<GeoFeature> features, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // Start binary import
        await using var writer = await conn.BeginBinaryImportAsync(@"
            COPY geofeatures (name, geom, properties) 
            FROM STDIN (FORMAT BINARY)", ct);

        foreach (var feature in features)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(feature.Name, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(feature.Geometry, NpgsqlDbType.Geometry, ct);
            await writer.WriteAsync(feature.Properties, NpgsqlDbType.Jsonb, ct);
        }

        await writer.CompleteAsync(ct);
    }
}