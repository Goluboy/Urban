using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Urban.Persistence.GeoJson.Interfaces;
using Urban.Persistence.GeoJson.Services;

namespace Urban.API.Import;

[ApiController]
[Route("api/[controller]")]
public class ImportController(IGeoFeatureRepository repo) : ControllerBase
{
    [HttpPost("geojsoninbody")]
    public async Task<IActionResult> ImportGeoJsonFromBody([FromBody] JsonElement geoJsonRoot, CancellationToken ct)
    {
        var geoJson = geoJsonRoot.GetRawText();
        var features = GeoJsonParser.ParseGeoJson(geoJson);
        await repo.BulkInsertAsync(geoJson, ct);
        return Ok($"{features.Count} features imported.");
    }

    [HttpPost("geojsoninform")]
    public async Task<IActionResult> ImportGeoJsonFromFile(IFormFile geoJsonRoot, CancellationToken ct)
    {
        if (geoJsonRoot is not { Length: > 0 } || !geoJsonRoot.ContentType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
            return BadRequest();

        await repo.ImportFromFileAsync(geoJsonRoot, ct);
        return Ok("features imported.");
    }
}