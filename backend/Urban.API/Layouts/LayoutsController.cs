using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using Urban.API.Layouts.DTOs;
using Urban.API.Layouts.DTOs.DTOGenerators;
using Urban.Application.Handlers;
using Urban.Application.Helpers;
using Urban.Application.Logging.Interfaces;

namespace Urban.API.Layouts;

[ApiController]
[Route("api/[controller]")]
public class LayoutsController(LayoutGenerationService layoutService, ILogger<LayoutsController> logger, IGeoLogger geoLogger) : ControllerBase 
{
    [HttpPost("generatelayouts")]
    [ProducesResponseType(typeof(LayoutsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    [RequestTimeout(300)] // 5 minutes
    public async Task<ActionResult<LayoutsResponse>> GenerateLayouts([FromBody] GenerateLayoutRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

        // Log request body
        var requestBody = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false });
        logger.LogInformation($"[{timestamp}] Client Request from {remoteIp}: {requestBody}");

        try
        {
            var points = request?.PolygonPoints?.Select(p => (p.Lng, p.Lat)).ToList();
            if (points == null || points.Count < 3)
            {
                var error = "Invalid polygon points";
                geoLogger.LogMessage($"Error: {error}");
                logger.LogError($"[{timestamp}] Client Error from {remoteIp}: {error} (after {sw.ElapsedMilliseconds}ms)");
                return BadRequest(new { error, logs = geoLogger.GetHtml() });
            }

            var (layouts, utmSystem, logs) = await layoutService.GenerateLayoutsAsync(points, request.MaxFloors, request.GrossFloorArea, ct);

            // Create a simplified response with all layouts
            var layoutsData = LayoutsResponseGenerator.CreateLayoutsResponse(layouts, utmSystem);

            sw.Stop();
            logger.LogInformation($"[{timestamp}] Client Success from {remoteIp}: Generated {layouts.Length} layouts in {sw.ElapsedMilliseconds}ms");

            return Ok(new { layouts = layoutsData, logs });
        }
        catch (ArgumentException ex)
        {
            sw.Stop();
            geoLogger.LogMessage($"Error: {ex.Message}");
            logger.LogError($"[{timestamp}] Client Error from {remoteIp}: {ex.Message} (after {sw.ElapsedMilliseconds}ms)");
            return BadRequest(new { error = ex.Message, logs = geoLogger.GetHtml() });
        }
        catch (TimeoutException ex)
        {
            sw.Stop();
            geoLogger.LogMessage($"Timeout Error: {ex.Message}");
            logger.LogError($"[{timestamp}] Client Timeout from {remoteIp}: {ex.Message} (after {sw.ElapsedMilliseconds}ms)");
            return StatusCode(408, new { error = "Request timeout", details = ex.Message, logs = geoLogger.GetHtml() });
        }
        catch (Exception ex)
        {
            sw.Stop();
            geoLogger.LogMessage($"Exception: {ex.Message}");
            geoLogger.LogMessage($"Stack Trace:\n{ex.StackTrace}");
            logger.LogError($"[{timestamp}] Client Error from {remoteIp}: {ex.Message} (after {sw.ElapsedMilliseconds}ms)\nStackTrace: {ex.StackTrace}");
            return StatusCode(500, new { error = ex.Message, details = ex.ToString(), logs = geoLogger.GetHtml() });
        }
    }
}