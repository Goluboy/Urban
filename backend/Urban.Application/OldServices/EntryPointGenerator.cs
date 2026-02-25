using NetTopologySuite.Geometries;
using Urban.Application.Helpers;

namespace Urban.Application.OldServices;

public static class EntryPointGenerator
{
    
    const double CollinearEdgesAngle = 30.0;
    const double ConcaveEntryPointAngle = 180 + CollinearEdgesAngle;
    
    public static Polygon EnsurePolygonRingContainsPoint(Polygon polygon, Point[] points)
    {
        var factory = polygon.Factory;
        var shellCoords = polygon.Shell.Coordinates;
        foreach (var pt in points)
            shellCoords = InsertPointOnRing(shellCoords, pt.Coordinate);
        var newShell = factory.CreateLinearRing(shellCoords);
        return factory.CreatePolygon(newShell);
    }

    private static Coordinate[] InsertPointOnRing(Coordinate[] ring, Coordinate pt)
    {
        var newCoords = new List<Coordinate>();
        for (int i = 0; i < ring.Length - 1; i++)
        {
            var c0 = ring[i];
            var c1 = ring[i + 1];
            newCoords.Add(c0);
            var segment = new LineSegment(c0, c1);
            if (segment.Distance(pt) < 1e-8 && !c0.Equals2D(pt) && !c1.Equals2D(pt))
                newCoords.Add(pt);
        }
        newCoords.Add(ring[0]);
        return newCoords.ToArray();
    }

    public static Point[] GenerateEntryPoints(Polygon polygon)
    {
        var result = new List<Point>();
        var coordinates = polygon.ExteriorRing.Coordinates;
        result.AddRange(GetConcaveEntryPoints(coordinates));
        var mergedEdges = MergeCollinearEdges(coordinates, CollinearEdgesAngle);
        foreach (var edge in mergedEdges)
        {
            var pathCoordinates = GetPathCoordinates(coordinates, edge.StartIndex, edge.EndIndex);
            var lineString = new LineString(pathCoordinates);
            int segments = (int)Math.Ceiling(lineString.Length / StreetGenerator.maxSegmentLength);
            for (int s = 1; s < segments; s++)
            {
                double t = (double)s / segments;
                result.Add(new Point(InterpolateAlongLineString(lineString, t)));
            }
        }
        return result.ToArray();
    }

    private static List<Point> GetConcaveEntryPoints(Coordinate[] coordinates)
    {
        var concavePoints = new List<Point>();
        if (coordinates.Length < 4) return concavePoints;
        bool isClockwise = GeometryUtils.IsPolygonClockwise(coordinates);
        for (int i = 1; i < coordinates.Length - 1; i++)
        {
            var prevPoint = coordinates[i - 1];
            var currentPoint = coordinates[i];
            var nextPoint = coordinates[(i + 1) % (coordinates.Length - 1)];
            var edge1 = new Coordinate(currentPoint.X - prevPoint.X, currentPoint.Y - prevPoint.Y);
            var edge2 = new Coordinate(nextPoint.X - currentPoint.X, nextPoint.Y - currentPoint.Y);
            var edge1Length = Math.Sqrt(edge1.X * edge1.X + edge1.Y * edge1.Y);
            var edge2Length = Math.Sqrt(edge2.X * edge2.X + edge2.Y * edge2.Y);
            if (edge1Length <= 80 || edge2Length <= 80) continue;
            var internalAngle = CalculateInternalPolygonAngle(prevPoint, currentPoint, nextPoint, isClockwise);
            var internalAngleDegrees = internalAngle * 180.0 / Math.PI;
            if (internalAngleDegrees > ConcaveEntryPointAngle)
                concavePoints.Add(new Point(currentPoint));
        }
        return concavePoints;
    }

    private static double CalculateInternalPolygonAngle(Coordinate prev, Coordinate current, Coordinate next, bool isClockwise)
    {
        var v1 = new Coordinate(prev.X - current.X, prev.Y - current.Y);
        var v2 = new Coordinate(next.X - current.X, next.Y - current.Y);
        var cross = v1.X * v2.Y - v1.Y * v2.X;
        var dot = v1.X * v2.X + v1.Y * v2.Y;
        var angle = Math.Atan2(Math.Abs(cross), dot);
        var isConcave = isClockwise ? cross < 0 : cross > 0;
        return isConcave ? 2 * Math.PI - angle : angle;
    }

    private static List<(Coordinate Start, Coordinate End, int StartIndex, int EndIndex)> MergeCollinearEdges(Coordinate[] coordinates, double maxAngleDegrees)
    {
        var mergedEdges = new List<(Coordinate Start, Coordinate End, int StartIndex, int EndIndex)>();
        if (coordinates.Length < 3) return mergedEdges;
        var maxAngleRadians = maxAngleDegrees * Math.PI / 180.0;
        int edgeStartIndex = 0;
        int currentEndIndex = 1;
        for (int i = 1; i < coordinates.Length - 1; i++)
        {
            var p1 = coordinates[i - 1];
            var p2 = coordinates[i];
            var p3 = coordinates[(i + 1) % (coordinates.Length - 1)];
            var v1 = new Coordinate(p2.X - p1.X, p2.Y - p1.Y);
            var v2 = new Coordinate(p3.X - p2.X, p3.Y - p2.Y);
            var angle = GeometryUtils.AngleBetween(v1, v2);
            if (angle > maxAngleRadians)
            {
                mergedEdges.Add((coordinates[edgeStartIndex], coordinates[currentEndIndex], edgeStartIndex, currentEndIndex));
                edgeStartIndex = currentEndIndex;
            }
            currentEndIndex = (i + 1) % (coordinates.Length - 1);
        }
        mergedEdges.Add((coordinates[edgeStartIndex], coordinates[currentEndIndex], edgeStartIndex, currentEndIndex));
        return mergedEdges;
    }

    private static Coordinate[] GetPathCoordinates(Coordinate[] coordinates, int startIndex, int endIndex)
    {
        var pathCoords = new List<Coordinate>();
        int current = startIndex;
        pathCoords.Add(coordinates[current]);
        while (current != endIndex)
        {
            current = (current + 1) % (coordinates.Length - 1);
            pathCoords.Add(coordinates[current]);
        }
        return pathCoords.ToArray();
    }

    private static Coordinate InterpolateAlongLineString(LineString lineString, double t)
    {
        var coords = lineString.Coordinates;
        double targetDistance = t * lineString.Length;
        double currentDistance = 0;
        for (int i = 0; i < coords.Length - 1; i++)
        {
            double dx = coords[i + 1].X - coords[i].X;
            double dy = coords[i + 1].Y - coords[i].Y;
            double segmentLength = Math.Sqrt(dx * dx + dy * dy);
            if (currentDistance + segmentLength >= targetDistance)
            {
                double segmentT = (targetDistance - currentDistance) / segmentLength;
                return new Coordinate(coords[i].X + segmentT * dx, coords[i].Y + segmentT * dy);
            }
            currentDistance += segmentLength;
        }
        return coords[coords.Length - 1];
    }
}