using NetTopologySuite.Geometries;
using Urban.Application.Helpers;
using Urban.Application.Logging;
using static Urban.Application.Helpers.GeometryUtils;

namespace Urban.Application.Services;

public static class PerimeterSectionGenerator
{
    static readonly Random _random = new();
    
    public static Polygon[] GeneratePerimeterSectionsForNonRectangle(Polygon block, double minWidth, double maxWidth, double minLength, double maxLength)
    {
        using (TimeLogger.Measure(nameof(GeneratePerimeterSectionsForNonRectangle)))
        {
            var sections = new List<Polygon>();
            var edges = block.GetEdges();
            var edgePointsMap = new Dictionary<LineSegment, List<Coordinate>>();

            // First, generate all edge points
            foreach (var edge in edges)
            {

                var edgePoints = GeneratePointsOnEdge(edge, minLength, maxLength, minLength * 1.2);
                edgePointsMap[edge] = edgePoints;
            }

            // Generate corner sections first without intersection checking
            var cornerSections = GenerateCornerSectionsFromEdgePoints(block, edges, edgePointsMap, minWidth, maxWidth);
            sections.AddRange(cornerSections);

            // Then generate edge sections with collision detection against corner sections
            foreach (var edge in edges)
            {
                if (!edgePointsMap.ContainsKey(edge)) continue;

                var edgePoints = edgePointsMap[edge];
                var edgeSections = CreateSectionsFromEdgePoints(block, edge, edgePoints, minWidth, maxWidth, sections);
                sections.AddRange(edgeSections);
            }

            return sections.ToArray();
        }
    }

    private static List<Coordinate> GeneratePointsOnEdge(LineSegment edge, double minLength, double maxLength, double minDistanceToCorner)
    {
        var points = new List<Coordinate>();
        var edgeLength = edge.Length;
        
        if (edgeLength < minDistanceToCorner * 2 + minLength)
        {
            points.Add(InterpolateOnEdge(edge.P0, edge.P1, 0.5));
            return points;
        }
            
        var currentPos = minDistanceToCorner;
        
        while (currentPos < edgeLength)
        {
            var remainingToEnd = edgeLength - currentPos;
            var nextSectionNeeds = (remainingToEnd > maxLength) ? minLength : 0;
            var effectiveMaxLength = Math.Min(maxLength, remainingToEnd - nextSectionNeeds);
            
            if (effectiveMaxLength < minLength) break;
            
            var segmentLength = minLength + _random.NextDouble() * (effectiveMaxLength - minLength);
            points.Add(InterpolateOnEdgeByDistance(edge.P0, edge.P1, currentPos));
            currentPos += segmentLength;
        }
        
        return points;
    }

    private static List<Polygon> CreateSectionsFromEdgePoints(Polygon block, LineSegment edge, List<Coordinate> edgePoints, double minWidth, double maxWidth, List<Polygon> existingSections)
    {
        var sections = new List<Polygon>();
        if (edgePoints.Count < 2) return sections;
        
        var edgeDir = GetDirection(edge.P0, edge.P1);
        var perpDir = GetPerpendicularDirection(edgeDir);
        var edgeMidpoint = InterpolateOnEdge(edge.P0, edge.P1, 0.5);
        
        if (!IsDirectionInward(block, edgeMidpoint, perpDir, minWidth))
        {
            perpDir = GetPerpendicularDirection(new Coordinate(-edgeDir.X, -edgeDir.Y));
        }
        
        for (int i = 0; i < edgePoints.Count - 1; i++)
        {
            var p1 = edgePoints[i];
            var p2 = edgePoints[i + 1];
            
            // Generate random width for this section
            var randomWidth = minWidth + _random.NextDouble() * (maxWidth - minWidth);
            
            var section = CreateSectionBetweenPoints(block, p1, p2, perpDir, randomWidth);
            if (section != null && section.Area > 0.01)
            {
                var hasIntersection = CheckSectionIntersections(section, existingSections, sections);
                
                if (hasIntersection)
                {
                    section = CreateSectionBetweenPoints(block, p1, p2, perpDir, minWidth);
                    if (section != null && section.Area > 0.01)
                    {
                        hasIntersection = CheckSectionIntersections(section, existingSections, sections);
                    }
                }
                
                // Only add if no intersection and section is inside block
                if (!hasIntersection && section != null && section.Area > 0.01 && block.Buffer(0.001).Contains(section))
                {
                    sections.Add(section);
                }
            }
        }
        
        return sections;
    }

    private static bool CheckSectionIntersections(Polygon section, List<Polygon> existingSections, List<Polygon> currentEdgeSections)
    {
        return existingSections.Any(existing => HasSignificantIntersection(section, existing)) ||
               currentEdgeSections.Any(current => HasSignificantIntersection(section, current));
    }

    private static Polygon? CreateSectionBetweenPoints(Polygon block, Coordinate p1, Coordinate p2, Coordinate perpDir, double width)
    {
        var innerP1 = new Coordinate(p1.X + perpDir.X * width, p1.Y + perpDir.Y * width);
        var innerP2 = new Coordinate(p2.X + perpDir.X * width, p2.Y + perpDir.Y * width);
        
        if (!block.Contains(new Point(innerP1)) || !block.Contains(new Point(innerP2)))
        {
            return null;
        }
        
        var sectionCoords = new[] { p1, p2, innerP2, innerP1, p1 };
        var section = new Polygon(new LinearRing(sectionCoords));
        
        if (!block.Contains(section))
        {
            var intersection = block.Intersection(section);
            if (intersection is Polygon poly && poly.Area > 0.01)
            {
                return poly;
            }
            return null;
        }
        
        return section;
    }

    private static List<Polygon> GenerateCornerSectionsFromEdgePoints(Polygon block, LineSegment[] edges, Dictionary<LineSegment, List<Coordinate>> edgePointsMap, double minWidth, double maxWidth)
    {
        var cornerSections = new List<Polygon>();
        var coordinates = block.ExteriorRing.Coordinates;
        
        // Skip the last coordinate as it's the same as the first (closed ring)
        for (int i = 0; i < coordinates.Length - 1; i++)
        {
            var corner = coordinates[i];
            
            // Find the two edges that meet at this corner
            var edge1 = i == 0 ? edges[^1] : edges[i - 1]; // Previous edge
            var edge2 = edges[i]; // Current edge
            
            // Get the edge points for these edges
            var edge1Points = edgePointsMap.ContainsKey(edge1) ? edgePointsMap[edge1] : new List<Coordinate>();
            var edge2Points = edgePointsMap.ContainsKey(edge2) ? edgePointsMap[edge2] : new List<Coordinate>();
            
            var cornerSection = CreateCornerSectionFromEdgePoints(block, edge1, edge2, corner, edge1Points, edge2Points, minWidth, maxWidth);
            if (cornerSection != null && block.Buffer(0.001).Contains(cornerSection))
            {
                cornerSections.Add(cornerSection);
            }
        }
        
        return cornerSections;
    }

    private static Polygon? CreateCornerSectionFromEdgePoints(Polygon block, LineSegment edge1, LineSegment edge2, Coordinate corner, List<Coordinate> edge1Points, List<Coordinate> edge2Points, double minWidth, double maxWidth)
    {
        // Find the closest edge points to the corner from each edge
        var point1 = FindClosestPointToCorner(edge1, edge1Points, corner);
        var point2 = FindClosestPointToCorner(edge2, edge2Points, corner);
        
        if (point1 == null || point2 == null) return null;
        
        var edge1Dir = GetEdgeDirectionFromCorner(edge1, corner);
        var edge2Dir = GetEdgeDirectionFromCorner(edge2, corner);
        
        var perp1Dir = GetPerpendicularDirection(edge1Dir);
        var perp2Dir = GetPerpendicularDirection(edge2Dir);
        
        if (!IsDirectionInward(block, point1, perp1Dir))
            perp1Dir = GetPerpendicularDirection(new Coordinate(-edge1Dir.X, -edge1Dir.Y));
        if (!IsDirectionInward(block, point2, perp2Dir))
            perp2Dir = GetPerpendicularDirection(new Coordinate(-edge2Dir.X, -edge2Dir.Y));
        
        // Generate random width for the corner section
        var cornerSectionWidth = minWidth + _random.NextDouble() * (maxWidth - minWidth);
        
        // First, find the intersection of the perpendicular lines to determine the actual geometry
        var perpEnd1 = new Coordinate(point1.X + perp1Dir.X * cornerSectionWidth, point1.Y + perp1Dir.Y * cornerSectionWidth);
        var perpEnd2 = new Coordinate(point2.X + perp2Dir.X * cornerSectionWidth, point2.Y + perp2Dir.Y * cornerSectionWidth);
        
        var perpIntersection = FindLineIntersection(point1, perpEnd1, point2, perpEnd2);
        
        Coordinate[] sectionCoords;
        
        if (perpIntersection != null)
        {
            var distance1 = Distance(point1, perpIntersection);
            var distance2 = Distance(point2, perpIntersection);
            var maxDistanceToIntersection = Math.Min(distance1, distance2);
            
            if (maxDistanceToIntersection < cornerSectionWidth + 2.0)
            {
                // Distance to intersection is less than corner width - use perpendicular intersection
                sectionCoords = new[] { corner, point1, perpIntersection, point2, corner };
            }
            else
            {
                // Distance is sufficient - use full corner section with parallel lines
                var parallelLine1End = new Coordinate(perpEnd1.X + edge1Dir.X * cornerSectionWidth * 2, perpEnd1.Y + edge1Dir.Y * cornerSectionWidth * 2);
                var parallelLine2End = new Coordinate(perpEnd2.X + edge2Dir.X * cornerSectionWidth * 2, perpEnd2.Y + edge2Dir.Y * cornerSectionWidth * 2);
                
                var parallelIntersection = FindLineIntersection(perpEnd1, parallelLine1End, perpEnd2, parallelLine2End);
                
                sectionCoords = parallelIntersection != null ? 
                    new[] { corner, point1, perpEnd1, parallelIntersection, perpEnd2, point2, corner } : 
                    new[] { corner, point1, perpEnd1, perpEnd2, point2, corner };
            }
        }
        else
        {
            // Fallback: perpendiculars don't intersect, use direct connection
            sectionCoords = new[] { corner, point1, perpEnd1, perpEnd2, point2, corner };
        }
        
        var section = new Polygon(new LinearRing(sectionCoords));
        return section;
    }
    
    private static Coordinate? FindClosestPointToCorner(LineSegment edge, List<Coordinate> edgePoints, Coordinate corner)
    {
        if (!edgePoints.Any()) return null;
        
        var startIsCorner = Distance(edge.P0, corner) < 0.001;
        var endIsCorner = Distance(edge.P1, corner) < 0.001;
        
        if (!startIsCorner && !endIsCorner) return null;
        
        return edgePoints.OrderBy(p => Distance(p, corner)).First();
    }

    private static Coordinate GetEdgeDirectionFromCorner(LineSegment edge, Coordinate corner)
    {
        var startIsCorner = Distance(edge.P0, corner) < 0.001;
        return startIsCorner ? GetDirection(edge.P0, edge.P1) : GetDirection(edge.P1, edge.P0);
    }


}