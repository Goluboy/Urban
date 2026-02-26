using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;
using System.Text.Json.Serialization;
using Urban.Application.Interfaces;
using Urban.Domain.Common;
using Urban.Domain.Geometry;
using Urban.Domain.Geometry.Data;
using Urban.Persistence.GeoJson.Services;

namespace Urban.Persistence.GeoJson;

/// <summary>
/// Repository used for bulk operations with data using Npgsql
/// </summary>
/// <param name="connectionString"></param>
public class GeoFeatureRepository(string? connectionString, ApplicationDbContext context, IGeoJsonParser geoJsonParser) : IGeoFeatureRepository
{
    public async Task<List<Restriction>> GetRestrictionsByType(string type, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(type))
            return new List<Restriction>();

        // Try parse to enum first for exact matching
        if (Enum.TryParse<RestrictionType>(type, ignoreCase: true, out var parsed))
        {
            var list = await context.Restrictions
                .AsNoTracking()
                .Where(r => r.Discriminator == parsed && (r.IsDeleted == null || r.IsDeleted == false))
                .ToListAsync(ct);

            // populate non-mapped properties
            foreach (var r in list)
                r.BoundingBox = r.Geometry?.EnvelopeInternal;

            return list;
        }

        // Fallback: compare string representation
        var fallbackList = await context.Restrictions
            .AsNoTracking()
            .Where(r => r.Discriminator.ToString() == type && (r.IsDeleted == null || r.IsDeleted == false))
            .ToListAsync(ct);

        foreach (var r in fallbackList)
            r.BoundingBox = r.Geometry?.EnvelopeInternal;

        return fallbackList;
    }

    public async Task<List<Restriction>> GetRestrictionsByType(RestrictionType type, CancellationToken ct = default)
    {
        var list = await context.Restrictions
            .AsNoTracking()
            .Where(r => r.Discriminator == type && (r.IsDeleted == null || r.IsDeleted == false))
            .ToListAsync(ct);

        foreach (var r in list)
            r.BoundingBox = r.Geometry?.EnvelopeInternal;

        return list;
    }

    public async Task<IList<Restriction>> GetNearestRestrictions(Geometry geometry, RestrictionType restrictionType, double distanceThreshold, CancellationToken ct = default)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));

        // Ensure SRID is set; default to 4326 if unknown
        if (geometry.SRID == 0)
            geometry.SRID = 4326;

        // Use EF Core with NetTopologySuite translation to PostGIS
        var query = context.Restrictions
            .AsNoTracking()
            .Where(r => r.Discriminator == restrictionType && r.Geometry != null)
            .Where(r => r.Geometry.Distance(geometry) < distanceThreshold)
            .OrderBy(r => r.Geometry.Distance(geometry));

        var list = await query.ToListAsync(ct);

        foreach (var r in list)
            r.BoundingBox = r.Geometry?.EnvelopeInternal;

        return list.ToList();
    }

    public async Task<int> BulkInsertAsync(string geoJson, string type, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(geoJson))
            return 0;

        // Parse GeoJSON into domain GeoFeature objects
        var features = geoJsonParser.ParseGeoJson(geoJson);
        if (features == null || features.Count == 0)
            return 0;

        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type is required", nameof(type));

        if (!Enum.TryParse<RestrictionType>(type, ignoreCase: true, out var discriminator))
            throw new ArgumentException($"Unknown restriction type: {type}", nameof(type));

        var now = DateTimeOffset.UtcNow;

        var restrictions = features.Select(f =>
        {
            var r = new Restriction(f)
            {
                Id = Guid.NewGuid(),
                Discriminator = discriminator,
                DateCreated = now,
                DateUpdated = null,
                DateDeleted = null,
                UserId = Guid.NewGuid(),
                IsDeleted = false
            };

            if (r.Geometry != null && r.Geometry.SRID == 0)
                r.Geometry.SRID = 4326;

            r.BoundingBox = r.Geometry?.EnvelopeInternal;
            return r;
        }).ToList();

        await context.AddRangeAsync(restrictions, ct);
        await context.SaveChangesAsync(ct);

        return restrictions.Count;
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