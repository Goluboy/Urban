using NetTopologySuite.Geometries;
using NetTopologySuite.Noding.Snapround;
using NetTopologySuite.Operation.Polygonize;
using Urban.Application.Helpers;
using Urban.Application.Logging;

namespace Urban.Application.Services;

public static class StreetGenerator
{

    public const double maxSegmentLength = 120.0;

    public static (Point[], LineString[], Polygon[]) SplitPolygonByStreets(Polygon plot, double projPairFactor = 1.0)
    {
        using (TimeLogger.Measure(nameof(SplitPolygonByStreets)))
        {
            var entryPoints = EntryPointGenerator.GenerateEntryPoints(plot);
            var plotWithPoints = EntryPointGenerator.EnsurePolygonRingContainsPoint(plot, entryPoints);

            //var streets = GenerateStreetsToCenter(plotWithPoints, entryPoints);
            //var streets = GenerateStreetsAsPerdendiculars(plotWithPoints, entryPoints);
            var streets = GenerateStreetsAsPerdendiculars3(plotWithPoints, entryPoints);
            
            if (!streets.Any())
                return (entryPoints, streets, new [] {plotWithPoints});

            var noder = new GeometryNoder(new PrecisionModel(1e9));
            
            var allLines =  plotWithPoints.GetEdgesAsLineStrings().Union(streets).ToArray();
            var nodedLines = noder.Node(allLines).ToArray();

            var polygonizer = new Polygonizer();        
            polygonizer.Add(nodedLines);
            
            var blocks = polygonizer.GetPolygons().Cast<Polygon>().ToArray();
            
            return (entryPoints, streets, blocks);
        }
    }


    public static LineString[] GenerateStreetsAsPerdendiculars2(Polygon plot, Point[] entryPoints, double projPairFactor = 1.0)
    {
        var remaining = entryPoints.ToList();
        var streets = new List<LineString>();

        while (remaining.Count > 0)
        {
            var bestPair = remaining.SelectMany(a => remaining.Where(b => !a.Equals(b))
                    .Select(b =>
                    {
                        var va = plot.GetInwardPerpVector(a);
                        var vb = plot.GetInwardPerpVector(b);
                        var dir = new Coordinate(b.X - a.X, b.Y - a.Y);
                        var avgAngle = (GeometryUtils.AngleBetween(va, dir) + GeometryUtils.AngleBetween(vb, new Coordinate(-dir.X, -dir.Y))) / 2;
                        return (a, b, avgAngle);
                    }))
                .OrderBy(t => t.avgAngle)
                .FirstOrDefault();

            var bestProj = streets.Any() ? remaining.SelectMany(entryPoint => streets.Select(street =>
                {
                    var perp = plot.GetInwardPerpVector(entryPoint);
                    var cross = ClosestPointOnLine(street, entryPoint.Coordinate);
                    var dir = new Coordinate(cross.X - entryPoint.X, cross.Y - entryPoint.Y);
                    var angle = GeometryUtils.AngleBetween(perp, dir);
                    return (entryPoint, cross, angle);
                }))
                .OrderBy(t => t.angle)
                .FirstOrDefault() : default;

            if (bestProj != default && (bestPair == default || bestProj.angle < bestPair.avgAngle * projPairFactor))
            {
                streets.Add(new LineString(new [] {bestProj.entryPoint.Coordinate, bestProj.cross}));
                remaining.Remove(bestProj.entryPoint);
            }
            else if (bestPair != default)
            {
                streets.Add(new LineString(new [] {bestPair.a.Coordinate, bestPair.b.Coordinate}));
                remaining.Remove(bestPair.a);
                remaining.Remove(bestPair.b);
            }
            else 
                break;
        }

        return streets.ToArray();
    }
    
    // New strategy with three choices at each step:
    // 1) Connect a pair of entry points
    // 2) Project an entry point to an existing street
    // 3) Connect an entry point directly to an existing cross (intersection) of streets
    // Constraint: options (1) and (2) must not place a street that passes within
    //             minCrossDistance of any existing cross
    public static LineString[] GenerateStreetsAsPerdendiculars3(
        Polygon plot,
        Point[] entryPoints,
        double minCrossDistance = 30.0)
    {
        using (TimeLogger.Measure("GenerateStreetsAsPerdendiculars3"))
        {
			var remaining = entryPoints.ToList();
			var builtStreets = new List<LineString>();
			// Prepare boundary edges as simple streets list
			var boundaryEdges = new List<LineString>();
			var bCoords = plot.ExteriorRing.Coordinates;
			for (int i = 0; i < bCoords.Length - 1; i++)
				boundaryEdges.Add(new LineString(new [] {bCoords[i], bCoords[i + 1]}));

            while (remaining.Count > 0)
            {
                var crosses = ComputeCrosses(builtStreets);
                var candidates = new List<Choice>();
                // point-to-point: from each remaining entry point to other remaining entry points and to existing crosses
                foreach (var a in remaining)
                {
                    // to other entry points
                    foreach (var b in entryPoints)
                    {
                        if (ReferenceEquals(a, b)) continue;
                        var line = new LineString(new [] {a.Coordinate, b.Coordinate});
                        if (!plot.Covers(line)) continue;
                        if (IsTooCloseToAnyCross(line, crosses, minCrossDistance)) continue;
						var score = EvaluateCandidateAngle(boundaryEdges, builtStreets, a.Coordinate, b.Coordinate, line);
                        candidates.Add(new Choice("pair", score, line, a, b, null));
                    }

                    // to existing crosses
                    foreach (var cross in crosses)
                    {
                        var line = new LineString(new [] {a.Coordinate, cross});
                        if (!plot.Covers(line)) continue;
						var score = EvaluateCandidateAngle(boundaryEdges, builtStreets, a.Coordinate, cross, line);
                        candidates.Add(new Choice("pair", score, line, a, null, cross));
                    }
                }
                // point-to-projection: project each remaining entry onto existing streets
                if (builtStreets.Any())
                {
                    foreach (var a in remaining)
                    {
                        foreach (var s in builtStreets)
                        {
                            var closest = ClosestPointOnLine(s, a.Coordinate);
                            var line = new LineString(new [] {a.Coordinate, closest});
                            if (!plot.Covers(line)) continue;
                            if (IsTooCloseToAnyCross(line, crosses, minCrossDistance)) continue;
							var score = EvaluateCandidateAngle(boundaryEdges, builtStreets, a.Coordinate, closest, line);
                            candidates.Add(new Choice("projection", score, line, a, null, closest));
                        }
                    }
                }

                if (!candidates.Any())
                    break;

                var best = candidates.OrderByDescending(c => c.Angle).First();
                

                if (best.Kind == "projection" && best.CrossEnd != null)
                {
                    SplitStreetAtPoint(builtStreets, best.CrossEnd!);
                }
                builtStreets.Add(best.Line);

                // Update remaining entry points
                remaining.Remove(best.RemoveA);
                if (best.RemoveB != null)
                    remaining.Remove(best.RemoveB);
            }

            return builtStreets.ToArray();
        }
    }

    private static List<Coordinate> ComputeCrosses(List<LineString> streets)
    {
        var crosses = new List<Coordinate>();
        for (int i = 0; i < streets.Count; i++)
        {
            for (int j = i + 1; j < streets.Count; j++)
            {
                var a = streets[i];
                var b = streets[j];
                var inter = a.Intersection(b);
                if (inter.IsEmpty)
                    continue;

                switch (inter)
                {
                    case Point p:
                        crosses.Add(p.Coordinate);
                        break;
                    case MultiPoint mp:
                        foreach (Point pt in mp.Geometries)
                            crosses.Add(pt.Coordinate);
                        break;
                    default:
                        // Overlapping line segments -> ignore for crosses
                        break;
                }
            }
        }
        return crosses;
    }

    private static void SplitStreetAtPoint(List<LineString> streets, Coordinate splitPoint)
    {
        const double epsilon = 1e-6;
        for (int i = 0; i < streets.Count; i++)
        {
            var street = streets[i];
            var coords = street.Coordinates;
            if (coords.Length < 2)
                continue;

            var start = coords[0];
            var end = coords[^1];
            var seg = new LineSegment(start, end);
            if (seg.Distance(splitPoint) <= epsilon)
            {
                // If split point is at an endpoint, don't split
                if (start.Distance(splitPoint) <= epsilon || end.Distance(splitPoint) <= epsilon)
                    return;

                // Replace this street with two segments
                streets.RemoveAt(i);
                streets.Add(new LineString(new [] {start, splitPoint}));
                streets.Add(new LineString(new [] {splitPoint, end}));
                return;
            }
        }
    }

    private static bool IsTooCloseToAnyCross(LineString candidate, IEnumerable<Coordinate> crosses, double minCrossDistance)
    {
        if (crosses == null)
            return false;

        // var candidateSegment = new LineSegment(candidate.GetCoordinateN(0), candidate.GetCoordinateN(candidate.NumPoints - 1));

        foreach (var cross in crosses)
        {
            var d = candidate.Distance(new Point(cross));
            if (d > 1 && d < minCrossDistance)
                return true;

            // // If the cross coincides with an endpoint, treat distance as zero
            // if (candidate.GetCoordinateN(0).Distance(cross) > 1e-9 || candidate.GetCoordinateN(candidate.NumPoints - 1).Distance(cross) < 1e-9)
            //     return true;

            // var distance = candidateSegment.Distance(cross);
            // if (distance < minCrossDistance)
            //     return true;
        }
        return false;
    }

    private static double EvaluateCandidateAngle(List<LineString> boundaryEdges, List<LineString> builtStreets, Coordinate from, Coordinate to, LineString candidate)
    {

        var streetsAndBounds = builtStreets.Union(boundaryEdges).ToList(); 
        
        // 90 is best, we return an angle in [0, PI/2]; larger is better when selecting, so we will sort descending
        var intersectAngle = GetMinAngleToIntersectedStreets(builtStreets, candidate);
        
        //var edgeAngle = GetMinAngleToIntersectedStreets(boundaryEdges, new LineString(new [] {from, to}));
        
        var outgoingAtFrom = GetMinAngleToOutgoingStreets(streetsAndBounds, from, to);
        // outgoing at 'to' only if 'to' is a cross/end present in existing streets
        var outgoingAtTo = GetMinAngleToOutgoingStreets(streetsAndBounds, to, from);
        // return new[] { edgeAngle, intersectAngle, outgoingAtFrom, outgoingAtTo }.Min();
        return new[] { intersectAngle, outgoingAtTo, outgoingAtFrom }.Min();
    }

    private static double GetMinAngleToIntersectedStreets(List<LineString> streets, LineString candidate)
    {
        if (!streets.Any())
            return double.MaxValue;

        double minAngle = double.MaxValue;
        var candCoords = candidate.Coordinates;
        var candDir = Normalize(new Coordinate(candCoords[^1].X - candCoords[0].X, candCoords[^1].Y - candCoords[0].Y));

        foreach (var street in streets)
        {
            var coords = street.Coordinates;
            for (int i = 0; i < coords.Length - 1; i++)
            {
                var seg = new LineSegment(coords[i], coords[i + 1]);
                var inter = seg.Intersection(new LineSegment(candCoords[0], candCoords[^1]));
                if (inter != null && 
                    inter.Distance(candidate.StartPoint.Coordinate) > 0.01 && 
                    inter.Distance(candidate.EndPoint.Coordinate) > 0.01 )
                {
                    var segDir = Normalize(new Coordinate(seg.P1.X - seg.P0.X, seg.P1.Y - seg.P0.Y));
                    var angle1 = GeometryUtils.AngleBetween(candDir, segDir);
                    var angle2 = GeometryUtils.AngleBetween(candDir, new Coordinate(-segDir.X, -segDir.Y));
                    var angle = Math.Min(angle1, angle2);
                    if (angle < minAngle)
                        minAngle = angle;
                }
            }
        }

        return minAngle;
    }

    // Removed: replaced by using GetMinAngleToIntersectedStreets(boundaryEdges, candidate)

    private static double GetMinAngleToOutgoingStreets(List<LineString> streets, Coordinate cross, Coordinate to)
    {
        if (!streets.Any())
            return double.MaxValue;

        var outgoingCandidateDir = Normalize(new Coordinate(to.X - cross.X, to.Y - cross.Y));
        double minAngle = double.MaxValue;
        const double epsilon = 1e-4;

        foreach (var street in streets)
        {
            var coords = street.Coordinates;
            for (int i = 0; i < coords.Length - 1; i++)
            {
                var a = coords[i];
                var b = coords[i + 1];
                
                var aIsCross = a.Distance(cross) < epsilon;
                var bIsCross = b.Distance(cross) < epsilon;
                
                if (!aIsCross && !bIsCross)
                    continue;

                var outgoingStreetDir = aIsCross ? new Coordinate(b.X - a.X, b.Y - a.Y) : new Coordinate(a.X - b.X, a.Y - b.Y);
                
                var angle = GeometryUtils.AngleBetween(outgoingCandidateDir, outgoingStreetDir);
                if (angle < minAngle)
                    minAngle = angle;
            }
        }

        return minAngle;
    }


    private static Coordinate Normalize(Coordinate v)
    {
        var len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
        if (len <= 1e-12)
            return new Coordinate(0, 0);
        return new Coordinate(v.X / len, v.Y / len);
    }

    private readonly record struct Choice(
        string Kind,
        double Angle,
        LineString Line,
        Point RemoveA,
        Point? RemoveB,
        Coordinate? CrossEnd
    );


    // Compute closest point on LineString manually
    public static Coordinate ClosestPointOnLine(LineString line, Coordinate pt)
    {
        var coords = line.Coordinates;
        if (coords.Length == 0)
            return pt;
        if (coords.Length == 1)
            return coords[0];

        // Initialize with the closer endpoint
        Coordinate best = coords[0].Distance(pt) <= coords[^1].Distance(pt) ? coords[0] : coords[^1];
        double minDist = best.Distance(pt);

        for (int i = 0; i < coords.Length - 1; i++)
        {
            var seg = new LineSegment(coords[i], coords[i + 1]);
            var proj = seg.ClosestPoint(pt);
            var dist = proj.Distance(pt);
            if (dist < minDist)
            {
                minDist = dist;
                best = proj;
            }
        }
        return best;
    }
}