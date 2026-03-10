using Microsoft.AspNetCore.Http;
using Urban.Application.Interfaces.Results;
using Urban.Domain.Common;
using Urban.Domain.Geometry.Data;

namespace Urban.Application.Interfaces;

/// <summary>
/// Repository used for bulk operations with data using Npgsql
/// </summary>
/// <param name="connectionString"></param>
public interface IGeoFeatureRepository
{
    /// <summary>
    /// Gets all restrictions of a given type that are not marked as deleted. This method uses raw SQL for performance and manually maps the results to Restriction objects, including
    /// </summary>
    /// <param name="type"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task<List<Restriction>> GetRestrictionsByType(string type, CancellationToken ct = default);

    /// <summary>
    /// Overload that accepts RestrictionType enum directly for convenience. Internally calls the string-based method to avoid code duplication.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task<List<Restriction>> GetRestrictionsByType(RestrictionType type, CancellationToken ct = default);

    /// <summary>
    /// Gets all restrictions of a given type that are within a specified distance from the provided geometry. Distance is calculated using PostGIS geography type for
    /// accurate meter-based measurements. Only non-deleted restrictions are returned, ordered by proximity to the input geometry. This method uses raw SQL for performance
    /// and assumes the "Geometry" column is indexed with a GIST index on the geography type for optimal spatial queries.
    /// </summary>
    /// <param name="geometry"></param>
    /// <param name="restrictionType"></param>
    /// <param name="distanceThreshold"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public Task<List<Restriction>> GetNearestRestrictions(NetTopologySuite.Geometries.Geometry geometry, RestrictionType restrictionType, double distanceThreshold, CancellationToken ct = default);

    /// <summary>
    /// Import GeoJSON data using raw SQL for efficient bulk insert.
    /// This method assumes the input GeoJSON is a valid FeatureCollection and that the "type" parameter corresponds to a valid RestrictionType enum value.
    /// Type must be mapped to <see cref="RestrictionType"/>
    /// </summary>
    /// <param name="geoJson">GeoJson data as string</param>
    /// <param name="type">Type must be mapped to <see cref="RestrictionType"/></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task<int> BulkInsertEntireJsonAsync(string geoJson, string type, CancellationToken ct = default);

    /// <summary>
    /// Splits the GeoJSON import into batches of 1,000 features to avoid memory issues with large files. Performs bulk inserts for each batch using Npgsql's binary COPY.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="type"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task<ImportResult> ImportGeoJsonStreamAsync(Stream stream, string type, CancellationToken ct);
}