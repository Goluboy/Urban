using Urban.Application.Interfaces.Results;
using Urban.Domain.Common;

namespace Urban.Application.Import.Interfaces;

/// <summary>
/// Provides services for importing GeoJSON data.
/// </summary>
/// <param name="geoFeatureRepository"></param>
/// <param name="logger"></param>
public interface IGeoJsonImportService
{
    /// <summary>
    /// Imports GeoJSON data from a stream and associates it with a specified type.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="type"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<ImportResult> ImportGeoJsonAsync(
        Stream stream,
        string type,
        CancellationToken ct);

    /// <summary>
    /// Overload that accepts a RestrictionType enum instead of a string, providing type safety and convenience.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="type"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<ImportResult> ImportGeoJsonAsync(
        Stream stream,
        RestrictionType type,
        CancellationToken ct);
}