using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.RegularExpressions;
using Urban.Application.Handlers;
using Urban.Application.Import.Interfaces;
using Urban.Application.Interfaces;
using Urban.Domain.Common;

namespace Urban.API.Import;

[ApiController]
[Route("api/[controller]")]
public class ImportController(
    IGeoFeatureRepository repo, 
    ILogger<ImportController> logger, 
    IGeoJsonImportService geoJsonImportService) : ControllerBase
{
    /// <summary>
    /// Import data from GeoJson in File
    /// </summary>
    /// <param name="file"></param>
    /// <param name="type">Discriminator will be  mapped to RestrictionType</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)] // 50 MB
    [HttpPost("geojsoninform")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ImportGeoJsonFromFile(
        IFormFile file,
        [FromQuery] string? type = null,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded or empty file" });

        if (file.Length > 50 * 1024 * 1024) // 50 MB limit
            return new StatusCodeResult(StatusCodes.Status413PayloadTooLarge);

        // Check file extension and content type
        var allowedExtensions = new[] { ".geojson", ".json" };
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(fileExtension))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType,
                new { error = $"Invalid file type. Allowed: {string.Join(", ", allowedExtensions)}" });

        var allowedContentTypes = new[] { "application/json", "application/geo+json" };
        if (!allowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType,
                new { error = $"Invalid Content-Type: {file.ContentType}. Allowed: {string.Join(", ", allowedContentTypes)}" });

        // Validate type which must be a valid RestrictionType enum value 
        if (!Enum.TryParse<RestrictionType>(type, ignoreCase: true, out var parsedType))
        {
            var allowedValues = string.Join(", ", Enum.GetNames<RestrictionType>());
            return BadRequest(new
            {
                error = $"Invalid type '{type}'. Allowed types: {allowedValues}"
            });
        }

        // Stream file to repository
        try
        {
            await using var stream = file.OpenReadStream();

            logger.LogInformation("Starting GeoJSON import: {FileName} ({Size} bytes), Type: {Type}",
                file.FileName, file.Length, type);

            var importResult = await geoJsonImportService.ImportGeoJsonAsync(
                stream,
                parsedType,
                ct);

            logger.LogInformation("Import completed: {Count} features imported from {FileName}",
                importResult.FeatureCount, file.FileName);

            return Ok(new
            {
                message = "Import successful",
                featuresImported = importResult.FeatureCount,
                fileName = file.FileName,
                type = type,
                processingTimeMs = importResult.ElapsedMilliseconds
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogWarning("GeoJSON import cancelled by client: {FileName}", file.FileName);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = "Import cancelled" });
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid GeoJSON format in {FileName}", file.FileName);
            return BadRequest(new { error = "Invalid GeoJSON format", details = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to import GeoJSON file: {FileName}", file.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Import failed. Check server logs for details." });
        }
    }

    /// <summary>
    /// Load files from Data folder
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("importfromdata")]
    public async Task<IActionResult> ImportFromData(CancellationToken ct = default)
    {
        var results = new List<object>();
        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");

        foreach (var kv in RestrictionsHandler.FileMap)
        {
            var rType = kv.Key;
            var fileName = kv.Value;
            var filePath = Path.Combine(dataDir, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                results.Add(new { type = rType.ToString(), file = fileName, status = "missing" });
                continue;
            }

            var json = await System.IO.File.ReadAllTextAsync(filePath, ct);
            var inserted = await repo.BulkInsertEntireJsonAsync(json, rType.ToString(), ct);
            results.Add(new { type = rType.ToString(), file = fileName, inserted });
        }

        return Ok(results);
    }

    [HttpGet]
    public async Task<ActionResult<List<GeoFeature>>> Get([FromQuery] string type, CancellationToken ct)
    {
        return Ok(await repo.GetRestrictionsByType(type, ct));
    }
}