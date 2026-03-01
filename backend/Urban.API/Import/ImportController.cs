using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Urban.Application.Interfaces;
using Urban.Domain.Common;
using Urban.Application.Handlers;

namespace Urban.API.Import;

[ApiController]
[Route("api/[controller]")]
public class ImportController(IGeoFeatureRepository repo) : ControllerBase
{
    /// <summary>
    /// Import data from GeoJson in body
    /// </summary>
    /// <param name="geoJsonRoot">GeoJson from body</param>
    /// <param name="type">Discriminator will be  mapped to RestrictionType</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("geojsoninbody")]
    public async Task<IActionResult> ImportGeoJsonFromBody([FromBody] JsonElement geoJsonRoot, string type, CancellationToken ct = default)
    {
        var geoJson = geoJsonRoot.GetRawText();
        await repo.BulkInsertAsync(geoJson, type, ct);
        return Ok("features imported.");
    }

    /// <summary>
    /// Import data from GeoJson in File
    /// </summary>
    /// <param name="geoJsonRoot"></param>
    /// <param name="type">Discriminator will be  mapped to RestrictionType</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    [HttpPost("geojsoninform")]
    public async Task<IActionResult> ImportGeoJsonFromFile(IFormFile geoJsonRoot, [FromQuery] string type, CancellationToken ct = default)
    {
        if (geoJsonRoot is not { Length: > 0 } || !geoJsonRoot.ContentType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
            return BadRequest();

        if (type is null || type == "")
            type = Path.GetFileNameWithoutExtension(geoJsonRoot.FileName);

        await repo.ImportFromFileAsync(geoJsonRoot, type, ct);
        return Ok("features imported.");
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
            var inserted = await repo.BulkInsertAsync(json, rType.ToString(), ct);
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