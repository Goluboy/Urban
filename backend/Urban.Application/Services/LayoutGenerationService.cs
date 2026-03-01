using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using Urban.Application.Helpers;
using Urban.Application.Logging.Interfaces;
using Urban.Application.Upgrades;
using Urban.Domain.Geometry;

namespace Urban.Application.Services;

public class LayoutGenerationService(LayoutGenerator generator, IGeoLogger geoLogger)
{
    public async Task<(BlockLayout[] Layouts, ProjectedCoordinateSystem UtmSystem, string Logs)> GenerateLayoutsAsync(
        IReadOnlyList<(double Lng, double Lat)> points,
        int? maxFloors,
        double? grossFloorArea,
        CancellationToken ct = default)
    {
        if (points == null || points.Count < 3)
            throw new ArgumentException("Polygon must contain at least 3 points", nameof(points));

        // Convert points to NTS coordinates
        var coordinates = points.Select(p => new Coordinate(p.Lng, p.Lat)).ToList();

        // Ensure ring is closed
        if (coordinates.Count == 0 || !coordinates[0].Equals2D(coordinates[^1]))
            coordinates.Add(coordinates[0]);

        var polygon = new Polygon(new LinearRing(coordinates.ToArray()));

        if (!polygon.IsValid)
            throw new ArgumentException("Invalid polygon geometry after construction");

        // Get UTM from users polygon
        var (polygonUtm, utmSystem) = CoordinatesConverter.ToUtm(polygon);

        // Generate layouts
        var layouts = await generator.GenerateLayouts((Polygon)polygonUtm, maxFloors, grossFloorArea);

        var logs = geoLogger.GetHtml();

        return (layouts, utmSystem, logs);
    }
}
