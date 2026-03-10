using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Urban.Application.Import.Interfaces;
using Urban.Application.Interfaces;
using Urban.Application.Interfaces.Results;
using Urban.Domain.Common;

namespace Urban.Application.Import;


public class GeoJsonImportService(IGeoFeatureRepository geoFeatureRepository, ILogger<GeoJsonImportService> logger) : IGeoJsonImportService
{
    public async Task<ImportResult> ImportGeoJsonAsync(
        Stream stream,
        string type,
        CancellationToken ct)
    {
        var importResult = await geoFeatureRepository.ImportGeoJsonStreamAsync(
            stream,
            type,
            ct);

        var stopwatch = Stopwatch.StartNew();

        stopwatch.Stop();

        logger.LogInformation(
            "Imported {Count} features of type '{Type}' in {Elapsed}ms",
            importResult, type, stopwatch.ElapsedMilliseconds);

        return importResult;
    }

    public async Task<ImportResult> ImportGeoJsonAsync(
        Stream stream,
        RestrictionType type,
        CancellationToken ct)
    {
        return await ImportGeoJsonAsync(stream, type.ToString(), ct);
    }
}