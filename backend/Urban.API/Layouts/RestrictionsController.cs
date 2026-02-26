using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Urban.API.Layouts.DTOs;
using Urban.Application.Handlers;
using Urban.Application.Helpers;

namespace Urban.API.Layouts;

[ApiController]
[Route("api/[controller]")]
public class RestrictionsController(ILogger<RestrictionsController> logger, RestrictionHandler restrictionHandler) : ControllerBase
{
    [HttpPost]
    [RequestTimeout(300)] // 5 minutes timeout
    public async Task<ActionResult<object>> GetRestrictions([FromBody] RestrictionsRequest request)
    {
        try
        {
            if (request?.Plot.ValueKind == JsonValueKind.Undefined)                
                return BadRequest(new { error = "Plot geometry is required" });
                
            // Convert JsonElement to Polygon using utility method
            var polygon = GeoJsonUtils.ConvertToPolygon(request!.Plot);

            if (!polygon.IsValid)
            {
                return BadRequest(new { error = "Invalid polygon geometry: polygon validation failed" });
            }

            // Convert polygon to UTM
            var (polygonUtm, utmSystem) = CoordinatesConverter.ToUtm(polygon);

            // Get restrictions for the polygon
            var restrictions = await restrictionHandler.GetNearestRestrictions(polygonUtm);
                
            logger.LogInformation("Found {Count} restrictions for the polygon", restrictions.Count);
                
            // Convert restrictions to double[][][] format
            var restrictionsData = restrictions.Select(r => new {
                restrictionType = r.Discriminator,
                coordinates = GeoJsonUtils.ConvertGeometryToCoordinates(CoordinatesConverter.FromUtm(r.Geometry, utmSystem))
            }).ToArray();

            logger.LogInformation("Converted {Count} restrictions to double[][][] format", restrictionsData.Length);

            return Ok(new { restrictions = restrictionsData });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Invalid GeoJSON format: {Message}", ex.Message);
            return BadRequest(new { error = $"Invalid GeoJSON format: {ex.Message}" });
        }
        catch (JsonException ex)
        {
            logger.LogWarning("JSON parsing error: {Message}", ex.Message);
            return BadRequest(new { error = $"JSON parsing error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting restrictions via API");
            return StatusCode(500, new { 
                error = ex.Message,
                details = ex.ToString()
            });
        }
    }


}