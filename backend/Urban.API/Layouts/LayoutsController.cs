using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using Urban.API.Layouts.DTOs;
using Urban.API.Layouts.DTOs.DTOGenerators;
using Urban.Application.Helpers;
using Urban.Application.Logging.Interfaces;
using Urban.Application.Upgrades;

namespace Urban.API.Layouts;

[ApiController]
[Route("api/[controller]")]
public class LayoutsController(NewLayoutGenerator newLayoutGenerator, ILogger<LayoutsController> logger, IGeoLogger geoLogger) : ControllerBase
{
    [HttpPost("generatelayouts")]
    [ProducesResponseType(typeof(LayoutsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    [RequestTimeout(300)] // 5 minutes
    public async Task<ActionResult<LayoutsResponse>> GenerateLayouts([FromBody] GenerateLayoutRequest request)
    {
        var sw = Stopwatch.StartNew();
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

        // Log request body
        var requestBody = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false });
        logger.LogInformation($"[{timestamp}] Client Request from {remoteIp}: {requestBody}");


        try
        {
            if (request?.PolygonPoints == null || request.PolygonPoints.Count < 3)
            {
                var error = "Invalid polygon points";
                geoLogger.LogMessage($"Error: {error}");
                logger.LogError($"[{timestamp}] Client Error from {remoteIp}: {error} (after {sw.ElapsedMilliseconds}ms)");
                return new JsonResult(new { error, logs = geoLogger.GetHtml() }) { StatusCode = 400 };
            }

            // Convert points to NTS coordinates
            var coordinates = request.PolygonPoints.Select(p => new Coordinate(p.Lng, p.Lat)).ToList();

            // Close the ring by adding the first point at the end
            coordinates.Add(coordinates[0]);

            // Create NTS polygon
            var polygon = new Polygon(new LinearRing(coordinates.ToArray()));
            if (!polygon.IsValid)
            {
                var error = "Invalid polygon geometry";
                geoLogger.LogMessage($"Error: {error}");
                logger.LogError($"[{timestamp}] Client Error from {remoteIp}: {error} (after {sw.ElapsedMilliseconds}ms)");
                return new JsonResult(new { error, logs = geoLogger.GetHtml() }) { StatusCode = 400 };
            }


            // Convert polygon to UTM
            var (polygonUtm, utmSystem) = CoordinatesConverter.ToUtm(polygon);

            var layouts = await newLayoutGenerator.GenerateLayouts((Polygon)polygonUtm, request.MaxFloors, request.GrossFloorArea);


            // Get the HTML logs from the geometry logger
            var htmlLogs = geoLogger.GetHtml();

            // Create a simplified response with all layouts
            var layoutsData = LayoutsResponseGenerator.CreateLayoutsResponse(layouts, utmSystem);

            sw.Stop();
            logger.LogInformation($"[{timestamp}] Client Success from {remoteIp}: Generated {layouts.Length} layouts in {sw.ElapsedMilliseconds}ms");

            return new JsonResult(new { layouts = layoutsData, logs = htmlLogs });
        }
        catch (TimeoutException ex)
        {
            sw.Stop();
            geoLogger.LogMessage($"Timeout Error: {ex.Message}");
            logger.LogError($"[{timestamp}] Client Timeout from {remoteIp}: {ex.Message} (after {sw.ElapsedMilliseconds}ms)");
            return new JsonResult(new { error = "Request timeout", details = ex.Message, logs = geoLogger.GetHtml() }) { StatusCode = 408 };
        }
        catch (Exception ex)
        {
            sw.Stop();
            geoLogger.LogMessage($"Exception: {ex.Message}");
            geoLogger.LogMessage($"Stack Trace:\n{ex.StackTrace}");
            logger.LogError($"[{timestamp}] Client Error from {remoteIp}: {ex.Message} (after {sw.ElapsedMilliseconds}ms)\nStackTrace: {ex.StackTrace}");
            return new JsonResult(new { error = ex.Message, details = ex.ToString(), logs = geoLogger.GetHtml() }) { StatusCode = 500 };
        }
    }
}