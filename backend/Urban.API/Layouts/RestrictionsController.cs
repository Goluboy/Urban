using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Urban.Application.Helpers;
using Urban.Application.Services;

namespace Urban.API.Layouts;

[ApiController]
[Route("api/[controller]")]
public class RestrictionsController(ILogger<RestrictionsController> logger) : ControllerBase
{
    public class RestrictionsRequest
    {
        [DefaultValue(@"{""type"":""Polygon"",""coordinates"":[[[60.579,56.811],[60.581,56.812],[60.583,56.811],[60.582,56.810],[60.579,56.811]]]}")]
        public JsonElement Plot { get; set; }
    }

    [HttpPost]
    [RequestTimeout(300)] // 5 minutes timeout
    public ActionResult<object> GetRestrictions([FromBody] RestrictionsRequest request)
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
            var restrictions = OknData.GetNearestRestrictions(polygonUtm);
                
            logger.LogInformation("Found {Count} restrictions for the polygon", restrictions.Count);
                
            // Convert restrictions to double[][][] format
            var restrictionsData = restrictions.Select(r => new {
                name = r.Name,
                restrictionType = r.RestrictionType,
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