using NetTopologySuite.Geometries;
using Urban.Application.Helpers;
using Urban.Application.Logging;

namespace Urban.Application.Services;

public static class SectionGenerator
{
    static readonly Random _random = new Random();
    
    private static readonly GeometryFactory _geometryFactory = GeometryFactory.Default;

    public static double[] GetAxes(Polygon polygon)
    {
        var coordinates = polygon.Coordinates;
        return Enumerable.Range(0, coordinates.Length - 1).Select(i =>
            {
                var a = coordinates[i];
                var b = coordinates[(i + 1) % coordinates.Length];
                return Tuple.Create(a.Distance(b), Math.Atan2(a.Y - b.Y, a.X - b.X));
            })
            .OrderByDescending(t => t.Item1).Take(2).Select(t => t.Item2).ToArray();//.Dump();

    }

    public static Bay[] SplitPolygonToBays(Polygon polygon, int floors, double minLength = 3.0)
    {
        var coords = polygon.ExteriorRing.Coordinates;
        var bays = new List<Bay>();

        Coordinate Interpolate(Coordinate a, Coordinate b, double t) =>
            new Coordinate(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        for (int i = 0; i < coords.Length - 1; i++)
        {
            var p1 = coords[i];
            var p2 = coords[i + 1];
            var length = p1.Distance(p2);

            int count = Math.Max(1, (int)Math.Floor(length / minLength));
            for (int j = 0; j < count; j++)
            {
                double t1 = (double)j / count;
                double t2 = (double)(j + 1) / count;
                var start = Interpolate(p1, p2, t1);
                var end = Interpolate(p1, p2, t2);
                bays.Add(new Bay { Start = start, End = end, Floors = floors, Height = LayoutGenerator.floorHeight * floors, EdgeIndex = i});
            }
        }
        return bays.ToArray();
    }

    public static Polygon GenerateRandomRotatedSquare(Polygon block, double width, double minLength, double maxLength, double[] axes)
    {
        var (centerX, centerY) = GetRandomCenter(block);
			
        double length = minLength + _random.NextDouble() * (maxLength - minLength);
        double angle = _random.Next(5) == 0
            ? _random.NextDouble() * 2 * Math.PI // Random angle in radians
            : axes[_random.Next(axes.Length)];

        var center = new Coordinate(centerX, centerY);
        return GeometryUtils.CreateRectangle(angle, center, width / 2, width / 2, length / 2, length / 2);
    }

    public static Polygon GenerateRandomRotatedSquare(Polygon block, double minSize, double maxSize, double[] doubles)
    {
        var (centerX, centerY) = GetRandomCenter(block);

        double width = minSize + _random.NextDouble() * (maxSize - minSize);
        double length = minSize + _random.NextDouble() * (maxSize - minSize);
        double angle = _random.Next(5) == 0
            ? _random.NextDouble() * 2 * Math.PI // Random angle in radians
            : doubles[_random.Next(doubles.Length)];

        var center = new Coordinate(centerX, centerY);
        return GeometryUtils.CreateRectangle(angle, center, width / 2, width / 2, length / 2, length / 2);
    }

    public static Polygon GenerateRectangleAlongPolygonEdgeSegment(
        Polygon polygon,
        double minHeight,
        double maxHeight,
        double minSegmentLength,
        double maxSegmentLength)
    {
        if (polygon == null || polygon.NumPoints < 4)
            throw new ArgumentException("Invalid polygon");

        var coords = polygon.ExteriorRing.Coordinates;
        int n = coords.Length - 1;
        var validEdges = new List<(Coordinate, Coordinate, double)>(); // (p1, p2, length)
			
        for (int i = 0; i < n; i++)
        {
            var pp1 = coords[i];
            var pp2 = coords[i + 1];
            double dx = pp2.X - pp1.X;
            double dy = pp2.Y - pp1.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length >= minSegmentLength)
                validEdges.Add((pp1, pp2, length));
        }

        if (validEdges.Count == 0)
            throw new InvalidOperationException("No edges long enough for given segment length.");
			
        var (fullP1, fullP2, fullLength) = validEdges[_random.Next(validEdges.Count)];

        double dxFull = fullP2.X - fullP1.X;
        double dyFull = fullP2.Y - fullP1.Y;

        double maxAvailableLength = Math.Min(fullLength, maxSegmentLength);
        double segmentLength = minSegmentLength + _random.NextDouble() * (maxAvailableLength - minSegmentLength);

        double startRatio = _random.NextDouble() * (fullLength - segmentLength) / fullLength;

        double segStartX = fullP1.X + dxFull * startRatio;
        double segStartY = fullP1.Y + dyFull * startRatio;

        double segEndX = segStartX + (dxFull / fullLength) * segmentLength;
        double segEndY = segStartY + (dyFull / fullLength) * segmentLength;

        var p1 = new Coordinate(segStartX, segStartY);
        var p2 = new Coordinate(segEndX, segEndY);
			
        double edgeDx = p2.X - p1.X;
        double edgeDy = p2.Y - p1.Y;
        double edgeLength = Math.Sqrt(edgeDx * edgeDx + edgeDy * edgeDy);
        double nx = -edgeDy / edgeLength;
        double ny = edgeDx / edgeLength;

        double height = minHeight + _random.NextDouble() * (maxHeight - minHeight);
			
        var r1 = new Coordinate(p1.X, p1.Y);
        var r2 = new Coordinate(p2.X, p2.Y);
        var r3 = new Coordinate(p2.X + nx * height, p2.Y + ny * height);
        var r4 = new Coordinate(p1.X + nx * height, p1.Y + ny * height);
        var rectCoords = new[] { r1, r2, r3, r4, r1 };

        return _geometryFactory.CreatePolygon(rectCoords);
    }

    public static Polygon GenerateRectangleOutsidePolygon(
        Polygon polygon,
        double minHeight,
        double maxHeight,
        double minSegmentLength,
        double maxSegmentLength)
    {
        var coords = polygon.ExteriorRing.Coordinates;
        int n = coords.Length - 1;

        var validEdges = new List<(Coordinate, Coordinate, double, int)>();
        for (int i = 0; i < n; i++)
        {
            var edgeStart = coords[i];
            var edgeEnd = coords[i + 1];
            double dx = edgeEnd.X - edgeStart.X;
            double dy = edgeEnd.Y - edgeStart.Y;
            double edgeLength = Math.Sqrt(dx * dx + dy * dy);

            if (edgeLength >= 0.5 * minSegmentLength)
                validEdges.Add((edgeStart, edgeEnd, edgeLength, i));
        }

        if (validEdges.Count == 0)
            throw new InvalidOperationException("No suitable edges found.");

        var (fullStart, fullEnd, fullLength, edgeIndex) = validEdges[_random.Next(validEdges.Count)];

        double dxFull = fullEnd.X - fullStart.X;
        double dyFull = fullEnd.Y - fullStart.Y;

        double segmentLength = minSegmentLength + _random.NextDouble() * (maxSegmentLength - minSegmentLength);
        double minOverlap = 0.5 * segmentLength;
        double maxOverhang = segmentLength - minOverlap;

        double edgeUnitX = dxFull / fullLength;
        double edgeUnitY = dyFull / fullLength;

        bool snapStart = _random.Next(3) == 0;
        bool snapEnd = _random.Next(3) == 0;

        double shiftAlongEdge;

        if (snapStart && snapEnd)
        {
            shiftAlongEdge = 0;
            segmentLength = fullLength;
        }
        else if (snapStart)
        {
            shiftAlongEdge = 0;
        }
        else if (snapEnd)
        {
            shiftAlongEdge = fullLength - segmentLength;
        }
        else
        {
            double maxStartShift = fullLength - minOverlap;
            shiftAlongEdge = -maxOverhang + _random.NextDouble() * (maxStartShift + maxOverhang);
        }

        double segStartX = fullStart.X + edgeUnitX * shiftAlongEdge;
        double segStartY = fullStart.Y + edgeUnitY * shiftAlongEdge;

        double segEndX = segStartX + edgeUnitX * segmentLength;
        double segEndY = segStartY + edgeUnitY * segmentLength;

        var segStart = new Coordinate(segStartX, segStartY);
        var segEnd = new Coordinate(segEndX, segEndY);

        int testIndex = (edgeIndex + 2) % n;
        var testPoint = coords[testIndex];

        double vx = segEnd.X - segStart.X;
        double vy = segEnd.Y - segStart.Y;
        double cross = vx * (testPoint.Y - segStart.Y) - vy * (testPoint.X - segStart.X);

        double baseLength = Math.Sqrt(vx * vx + vy * vy);
        double nx = -vy / baseLength;
        double ny = vx / baseLength;
        if (cross > 0)
        {
            nx = -nx;
            ny = -ny;
        }

        double height = minHeight + _random.NextDouble() * (maxHeight - minHeight);

        var r1 = new Coordinate(segStart.X, segStart.Y);
        var r2 = new Coordinate(segEnd.X, segEnd.Y);
        var r3 = new Coordinate(segEnd.X + nx * height, segEnd.Y + ny * height);
        var r4 = new Coordinate(segStart.X + nx * height, segStart.Y + ny * height);

        return _geometryFactory.CreatePolygon(new[] { r1, r2, r3, r4, r1 });
    }


    
    public static Polygon CreateInnerRectangleBlock(Polygon plot)
    {
        if (plot.IsEmpty)
            throw new Exception("A polygon is empty");

        var coords = plot.ExteriorRing.Coordinates;
        double bestArea = 0;
        Polygon bestRectangle = null;
        
        // Use edge orientations of the polygon
        var angles = new List<double>();
        for (int i = 0; i < coords.Length - 1; i++)
        {
            var a = coords[i];
            var b = coords[i + 1];
            double angle = Math.Atan2(b.Y - a.Y, b.X - a.X);
            // Normalize angle to [0, PI)
            angle = ((angle % Math.PI) + Math.PI) % Math.PI;
            if (!angles.Any(existing => Math.Abs(existing - angle) < 1e-6))
                angles.Add(angle);
        }
        
        // Sample 5 points inside the polygon
        var samplePoints = new List<Coordinate>();
        samplePoints.Add(plot.Centroid.Coordinate); // Always include centroid
        
        // Add 4 more random points
        var minX = plot.EnvelopeInternal.MinX;
        var minY = plot.EnvelopeInternal.MinY;
        var maxX = plot.EnvelopeInternal.MaxX;
        var maxY = plot.EnvelopeInternal.MaxY;
        
        for (int i = 0; i < 4; i++)
        {
            Coordinate point;
            do
            {
                double x = minX + _random.NextDouble() * (maxX - minX);
                double y = minY + _random.NextDouble() * (maxY - minY);
                point = new Coordinate(x, y);
            } while (!plot.Contains(new Point(point)));
            samplePoints.Add(point);
        }
        
        foreach (double angle in angles)
        {
            foreach (var center in samplePoints)
            {
                // Find maximum possible rectangle for this orientation and center
                var rectangle = FindMaxRectangleForOrientation(plot, angle, center);
                if (rectangle != null)
                {
                    double area = rectangle.Area;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        bestRectangle = rectangle;
                    }
                }
            }
        }
        
        return bestRectangle;
    }
    
    private static Polygon FindMaxRectangleForOrientation(Polygon block, double angle, Coordinate center)
    {
        double step = GeometryUtils.CalculateCoarseStepSize(block);
        
        // Phase 1: Extend the whole rectangle simultaneously
        double currentSize = 0;
        
        while (true)
        {
            // Try to extend all directions by one step simultaneously
            var testRectangle = GeometryUtils.CreateRectangle(angle, center, currentSize + step, currentSize + step, currentSize + step, currentSize + step);
            if (GeometryUtils.IsValidContainedRectangle(testRectangle, block))
            {
                currentSize += step;
            }
            else
            {
                break; // Can't extend further
            }
        }
        
        // Set initial rectangle dimensions
        double currentLeft = currentSize, currentRight = currentSize, currentDown = currentSize, currentUp = currentSize;
        
        // Phase 2: Try to extend each direction individually to maximum
        bool improved = true;
        while (improved)
        {
            improved = false;
            
            // Try to extend left
            var maxLeft = GeometryUtils.FindMaxExtensionInDirection(block, angle, center, currentLeft + step, currentRight, currentDown, currentUp, -1, 0);
            if (maxLeft > currentLeft)
            {
                currentLeft = maxLeft;
                improved = true;
            }
            
            // Try to extend right
            var maxRight = GeometryUtils.FindMaxExtensionInDirection(block, angle, center, currentLeft, currentRight + step, currentDown, currentUp, 1, 0);
            if (maxRight > currentRight)
            {
                currentRight = maxRight;
                improved = true;
            }
            
            // Try to extend down
            var maxDown = GeometryUtils.FindMaxExtensionInDirection(block, angle, center, currentLeft, currentRight, currentDown + step, currentUp, 0, -1);
            if (maxDown > currentDown)
            {
                currentDown = maxDown;
                improved = true;
            }
            
            // Try to extend up
            var maxUp = GeometryUtils.FindMaxExtensionInDirection(block, angle, center, currentLeft, currentRight, currentDown, currentUp + step, 0, 1);
            if (maxUp > currentUp)
            {
                currentUp = maxUp;
                improved = true;
            }
        }
        
        // Create final rectangle
        var rectangle = GeometryUtils.CreateRectangle(angle, center, currentLeft, currentRight, currentDown, currentUp);
        
        // Verify the rectangle is contained and has positive area
        if (GeometryUtils.IsValidContainedRectangle(rectangle, block))
        {
            return rectangle;
        }
        
        return null;
    }
    
    
    public static Polygon[] GenerateUrbanBlockInRectangle(
        Polygon rectangle,
        double minWidth = 12, double maxWidth = 20,
        double minLength = 24, double maxLength = 40)
    {
        var sections = new List<Polygon>();
        var coords = rectangle.ExteriorRing.Coordinates;
        
        // Get the 4 corners of the rectangle (excluding the last duplicate point)
        var corners = new[] { coords[0], coords[1], coords[2], coords[3] };
        
        // Calculate rectangle center to determine inward direction
        var centerX = coords.Average(c => c.X);
        var centerY = coords.Average(c => c.Y);
        var center = new Coordinate(centerX, centerY);
        var centerPoint = new Point(centerX, centerY);
        
        // Process all edges in a cycle
        for (int edgeIndex = 0; edgeIndex < 4; edgeIndex++)
        {
            var startPoint = corners[edgeIndex];
            var endPoint = corners[(edgeIndex + 1) % 4];
            
            // Calculate edge direction and length
            double edgeLength = startPoint.Distance(endPoint);
            double dx = endPoint.X - startPoint.X;
            double dy = endPoint.Y - startPoint.Y;
            double edgeDirX = dx / edgeLength;
            double edgeDirY = dy / edgeLength;
            
            // Calculate perpendicular direction (pointing inward)
            double perpDirX = -edgeDirY;
            double perpDirY = edgeDirX;
            
            // Ensure perpendicular direction points inward
            var testPoint = new Coordinate(
                startPoint.X + perpDirX * 10,
                startPoint.Y + perpDirY * 10
            );
            var testPointGeometry = new Point(testPoint);
            
            // If test point is further from center than the start point, flip direction
            if (testPointGeometry.Distance(centerPoint) > startPoint.Distance(center))
            {
                perpDirX = edgeDirY;
                perpDirY = -edgeDirX;
            }
            
            // Determine starting position for this edge
            Coordinate currentPos;
            if (edgeIndex == 0)
            {
                // First edge starts from the corner
                currentPos = new Coordinate(startPoint.X, startPoint.Y);
            }
            else if (sections.Count > 0)
            {
                // Subsequent edges start from the last section's end point
                var lastSection = sections[sections.Count - 1];
                var lastSectionCoords = lastSection.ExteriorRing.Coordinates;
                var lastSectionEnd = lastSectionCoords[1]; // c2 is the end point
                var lastSectionCorner = lastSectionCoords[2]; // c3 is the corner point (end + width)
                
                // Check if the last section's end point is close to the start of this edge
                // double distanceToStart = lastSectionEnd.Distance(startPoint);
                double distanceToEnd = lastSectionEnd.Distance(endPoint);
                

                
                // Calculate where the last section's corner intersects with the new edge
                // We need to project the corner point onto the new edge
                var lastSectionCornerPoint = lastSectionCoords[2]; // c3 is the corner point (end + width)
                
                // Project the corner point onto the new edge line
                // This gives us the closest point on the new edge to the corner
                double edgeVectorX = endPoint.X - startPoint.X;
                double edgeVectorY = endPoint.Y - startPoint.Y;
                double newEdgeLength = Math.Sqrt(edgeVectorX * edgeVectorX + edgeVectorY * edgeVectorY);
                
                if (newEdgeLength > 0)
                {
                    double edgeUnitX = edgeVectorX / newEdgeLength;
                    double edgeUnitY = edgeVectorY / newEdgeLength;
                    
                    // Vector from start point to corner
                    double toCornerX = lastSectionCornerPoint.X - startPoint.X;
                    double toCornerY = lastSectionCornerPoint.Y - startPoint.Y;
                    
                    // Project onto edge direction
                    double projection = toCornerX * edgeUnitX + toCornerY * edgeUnitY;
                    
                    // Clamp to edge bounds
                    projection = Math.Max(0, Math.Min(projection, newEdgeLength));
                    
                    // Calculate projected point
                    double projectedX = startPoint.X + edgeUnitX * projection;
                    double projectedY = startPoint.Y + edgeUnitY * projection;
                    
                    currentPos = new Coordinate(projectedX, projectedY);

                }
                else
                {
                    // Fallback to corner point if edge has zero length
                    currentPos = new Coordinate(lastSectionCornerPoint.X, lastSectionCornerPoint.Y);
                }                    
            }
            else
            {
                // No sections created yet, start from corner
                currentPos = new Coordinate(startPoint.X, startPoint.Y);
            }
            
            // Calculate remaining length from current position to end of this edge
            double remainingLength = endPoint.Distance(currentPos);
            
            // For the last edge, calculate distance to first section
            double targetDistance = remainingLength;
            if (edgeIndex == 3 && sections.Count > 0)
            {
                // Get the first section to calculate where to end
                var firstSection = sections[0];
                var firstSectionCoords = firstSection.ExteriorRing.Coordinates;
                var firstSectionCorner = firstSectionCoords[3]; // c4 is the corner point (start + width)
                
                // Project the first section's corner point onto the current edge
                double edgeVectorX = endPoint.X - startPoint.X;
                double edgeVectorY = endPoint.Y - startPoint.Y;
                double lastEdgeLength = Math.Sqrt(edgeVectorX * edgeVectorX + edgeVectorY * edgeVectorY);
                
                if (lastEdgeLength > 0)
                {
                    double edgeUnitX = edgeVectorX / lastEdgeLength;
                    double edgeUnitY = edgeVectorY / lastEdgeLength;
                    
                    // Vector from start point to first section corner
                    double toFirstSectionX = firstSectionCorner.X - startPoint.X;
                    double toFirstSectionY = firstSectionCorner.Y - startPoint.Y;
                    
                    // Project onto edge direction
                    double projection = toFirstSectionX * edgeUnitX + toFirstSectionY * edgeUnitY;
                    
                    // Clamp to edge bounds
                    projection = Math.Max(0, Math.Min(projection, lastEdgeLength));
                    
                    // Calculate projected point
                    double projectedX = startPoint.X + edgeUnitX * projection;
                    double projectedY = startPoint.Y + edgeUnitY * projection;
                    
                    // Calculate distance from current position to projected point
                    targetDistance = currentPos.Distance(new Coordinate(projectedX, projectedY));
                }
            }
            
            
            // Process this edge
            while (targetDistance > 0)
            {
                double sectionLength;
                
                // Check if there's enough space left for another section after this one
                if (targetDistance < minLength * 2)
                {
                    // Not enough space for another section, make this one go to the target
                    sectionLength = targetDistance;
                }
                else if (targetDistance < maxLength)
                {
                    // Less than max length but enough for another section, use remaining distance
                    sectionLength = targetDistance;
                }
                else
                {
                    // Plenty of space, generate random length
                    sectionLength = minLength + _random.NextDouble() * (maxLength - minLength);
                    sectionLength = Math.Min(sectionLength, targetDistance);
                }

                // Skip if section is too small
                if (sectionLength < 1e-6)
                {
                    break;
                }

                // Calculate maximum possible width that will fit
                double maxPossibleWidth = CalculateMaxWidthForSection(rectangle, currentPos, sectionLength, edgeDirX, edgeDirY, perpDirX, perpDirY, maxWidth);
                
                // Use the smaller of desired width or maximum possible width
                double desiredWidth = minWidth + _random.NextDouble() * (maxWidth - minWidth);
                double sectionWidth = Math.Min(desiredWidth, maxPossibleWidth);
                
                // Only create section if we can fit at least minimum width
                if (sectionWidth >= minWidth)
                {
                    // Create section coordinates - ensure they start exactly at currentPos
                    var sectionStart = new Coordinate(currentPos.X, currentPos.Y);
                    var sectionEnd = new Coordinate(
                        currentPos.X + edgeDirX * sectionLength,
                        currentPos.Y + edgeDirY * sectionLength
                    );

                    // Create section rectangle (pointing inward)
                    var c1 = new Coordinate(sectionStart.X, sectionStart.Y);
                    var c2 = new Coordinate(sectionEnd.X, sectionEnd.Y);
                    var c3 = new Coordinate(c2.X + perpDirX * sectionWidth, c2.Y + perpDirY * sectionWidth);
                    var c4 = new Coordinate(c1.X + perpDirX * sectionWidth, c1.Y + perpDirY * sectionWidth);

                    var section = new Polygon(new LinearRing(new[] { c1, c2, c3, c4, c1 }));

                    sections.Add(section);
                    // Update position for next section - ensure perfect alignment
                    currentPos = new Coordinate(sectionEnd.X, sectionEnd.Y);
                    
                    // Update target distance for next iteration
                    if (edgeIndex == 3 && sections.Count > 0)
                    {
                        // For last edge, recalculate  distance to first section
                        var firstSection = sections[0];
                        var firstSectionCoords = firstSection.ExteriorRing.Coordinates;
                        var firstSectionCorner = firstSectionCoords[3]; // c4 is the corner point (start + width)
                        
                        // Project the first section's corner point onto the current edge
                        double edgeVectorX = endPoint.X - startPoint.X;
                        double edgeVectorY = endPoint.Y - startPoint.Y;
                        double lastEdgeLength = Math.Sqrt(edgeVectorX * edgeVectorX + edgeVectorY * edgeVectorY);
                        
                        if (lastEdgeLength > 0)
                        {
                            double edgeUnitX = edgeVectorX / lastEdgeLength;
                            double edgeUnitY = edgeVectorY / lastEdgeLength;
                            
                            double toFirstSectionX = firstSectionCorner.X - startPoint.X;
                            double toFirstSectionY = firstSectionCorner.Y - startPoint.Y;
                            
                            double projection = toFirstSectionX * edgeUnitX + toFirstSectionY * edgeUnitY;
                            projection = Math.Max(0, Math.Min(projection, lastEdgeLength));
                            
                            double projectedX = startPoint.X + edgeUnitX * projection;
                            double projectedY = startPoint.Y + edgeUnitY * projection;
                            
                            targetDistance = currentPos.Distance(new Coordinate(projectedX, projectedY));
                        }
                    }
                    else
                    {
                        // For other edges, use remaining length to edge end
                        targetDistance = endPoint.Distance(currentPos);
                    }
                }
                else
                {
                    break; // Can't fit even minimum width, stop generating
                }
            }
        }
        
        return sections.ToArray();
    }

    private static double CalculateMaxWidthForSection(Polygon rectangle, Coordinate startPos, double sectionLength, 
        double edgeDirX, double edgeDirY, double perpDirX, double perpDirY, double maxWidth)
    {
        // Binary search to find maximum width that fits
        double minWidth = 0;
        double maxPossibleWidth = maxWidth;
        double tolerance = 0.1;
        
        while (maxPossibleWidth - minWidth > tolerance)
        {
            double testWidth = (minWidth + maxPossibleWidth) / 2;
            
            // Create test section with this width
            var sectionStart = new Coordinate(startPos.X, startPos.Y);
            var sectionEnd = new Coordinate(
                startPos.X + edgeDirX * sectionLength,
                startPos.Y + edgeDirY * sectionLength
            );
            
            var c1 = new Coordinate(sectionStart.X, sectionStart.Y);
            var c2 = new Coordinate(sectionEnd.X, sectionEnd.Y);
            var c3 = new Coordinate(c2.X + perpDirX * testWidth, c2.Y + perpDirY * testWidth);
            var c4 = new Coordinate(c1.X + perpDirX * testWidth, c1.Y + perpDirY * testWidth);
            
            var testSection = new Polygon(new LinearRing(new[] { c1, c2, c3, c4, c1 }));
            
            if (rectangle.Intersects(testSection))
            {
                minWidth = testWidth; // This width fits, try larger
            }
            else
            {
                maxPossibleWidth = testWidth; // This width doesn't fit, try smaller
            }
        }
        
        return minWidth; // Return the largest width that fits
    }

    private static (double centerX, double centerY) GetRandomCenter(Polygon block)
    {
        var minX = block.Coordinates.Min(c => c.X);
        var minY = block.Coordinates.Min(c => c.Y);
        var maxX = block.Coordinates.Max(c => c.X);
        var maxY = block.Coordinates.Max(c => c.Y);
        double centerX, centerY;
        do
        {
            centerX = minX + _random.NextDouble() * (maxX - minX);
            centerY = minY + _random.NextDouble() * (maxY - minY);
        } 
        while (!block.Contains(new Point(centerX, centerY)));
        return (centerX, centerY);
    }

    public static Polygon[] GenerateStandaloneSections(Polygon block, int width, int minLength, int maxLength)
    {
        using (TimeLogger.Measure(nameof(GenerateStandaloneSections)))
        {
            var axes = GetAxes(block);
            var bestArea = 0.0;
            var bestSections = new List<Polygon>();

            for (int i = 0; i < 100; i++)
            {
                var sections = new List<Polygon>();
                int t = 0;
                while (t < 100)
                {
                    t++;
                    var square = GenerateRandomRotatedSquare(block, width, minLength, maxLength, axes);
                    if (!block.Contains(square) || sections.Any(b => b.Distance(square) < 40))
                        continue;

                    sections.Add(square);
                }

                var area = sections.Sum(b => b.Area);
                if (area > bestArea)
                {
                    bestSections = sections;
                    bestArea = area;
                }
            }

            return bestSections.ToArray();
        }
    }
}