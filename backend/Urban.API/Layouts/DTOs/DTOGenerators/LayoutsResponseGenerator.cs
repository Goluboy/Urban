using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using Urban.Application.Helpers;
using Urban.Application.Services;
using Urban.Domain.Geometry;

namespace Urban.API.Layouts.DTOs.DTOGenerators;

public class LayoutsResponseGenerator
{
    public static object[] CreateLayoutsResponse(BlockLayout[] layouts, ProjectedCoordinateSystem utmSystem)
    {
        if (layouts == null || layouts.Length == 0)
            return Array.Empty<object>();

        return layouts.Select(layout => new
        {
            name = layout.Name,
            sections = (layout.Sections ?? Array.Empty<Section>()).Select(s => new
            {
                polygon = s.Polygon != null ? s.Polygon.ToDoubleArrayWgs84(utmSystem) : Array.Empty<double[][]>(),
                floors = s.Floors,
                height = s.Height,
                appartmetsArea = s.AppartmetsArea,
                commercialArea = s.CommercialArea,
                parkingPlaces = s.ParkingPlaces,
                residents = s.Residents,
                kindergardenPlaces = s.KindergardenPlaces,
                schoolPlaces = s.SchoolPlaces,
                bays = (s.Bays ?? Array.Empty<Bay>()).Select(b => new
                {
                    polygon = new[] { GetBayCoordinatesWgs84(b, utmSystem) },
                    shadowHeight = Math.Min(b.ResultShadowHeight, s.Height)
                }).ToArray()
            }).ToArray(),
            insolation = layout.Insolation,
            cost = layout.Cost,
            builtUpArea = layout.BuiltUpArea,
            usefulArea = layout.UsefulArea,
            value = layout.Value,
            streets = (layout.Streets ?? Array.Empty<LineString>())
                .SelectMany(street =>
                {
                    var wgs84Street = CoordinatesConverter.FromUtm(street, utmSystem);
                    return ((LineString)wgs84Street).Coordinates.Select(c => new[] { c.X, c.Y }).ToArray();
                })
                .ToArray()
        }).ToArray();
    }

    private static double[][] GetBayCoordinatesWgs84(Bay bay, ProjectedCoordinateSystem utmSystem)
    {
        // Offset by 0.5 meters perpendicular to the bay
        var dx = bay.End.X - bay.Start.X;
        var dy = bay.End.Y - bay.Start.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        var ox = -dy / len * 0.5;
        var oy = dx / len * 0.5;

        var p1 = new Coordinate(bay.Start.X + ox, bay.Start.Y + oy);
        var p2 = new Coordinate(bay.End.X + ox, bay.End.Y + oy);
        var p3 = new Coordinate(bay.End.X - ox, bay.End.Y - oy);
        var p4 = new Coordinate(bay.Start.X - ox, bay.Start.Y - oy);

        var coords = new[] { p1, p2, p3, p4, p1 };

        // Create polygon and convert to WGS84, then to double[][]
        var polygon = new Polygon(new LinearRing(coords));
        var wgs84Polygon = CoordinatesConverter.FromUtm(polygon, utmSystem);

        // Return first ring (exterior)
        return ((Polygon)wgs84Polygon).ToDoubleArray()[0];
    }
}