using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Urban.Application.Interfaces;
using Urban.Domain.Common;
using Urban.Persistence.GeoJson.Services;

namespace Urban.API.Import;

[ApiController]
[Route("api/[controller]")]
public class ImportController(IGeoFeatureRepository repo) : ControllerBase
{
    [HttpPost("geojsoninbody")]
    public async Task<IActionResult> ImportGeoJsonFromBody([FromBody] JsonElement geoJsonRoot, string type, CancellationToken ct)
    {
        var geoJson = geoJsonRoot.GetRawText();
        var features = GeoJsonParser.ParseGeoJson(geoJson);
        await repo.BulkInsertAsync(geoJson, type, ct);
        return Ok($"{features.Count} features imported.");
    }

    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    [HttpPost("geojsoninform")]
    public async Task<IActionResult> ImportGeoJsonFromFile(IFormFile geoJsonRoot, [FromQuery] string type, CancellationToken ct)
    {
        if (geoJsonRoot is not { Length: > 0 } || !geoJsonRoot.ContentType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
            return BadRequest();

        if (type is null || type == "")
            type = Path.GetFileNameWithoutExtension(geoJsonRoot.FileName);

        await repo.ImportFromFileAsync(geoJsonRoot, type, ct);
        return Ok("features imported.");
    }

    [HttpDelete("emptygeotable")]
    public async Task<IActionResult> EmptyGeoTable(CancellationToken ct)
    {
        return Ok("Nothing.");
    }

    [HttpGet]
    public async Task<ActionResult<List<GeoFeature>>> Get([FromQuery] string type, CancellationToken ct)
    {
        return Ok(await repo.GetGeoFeaturesByType(type, ct));
    }
}