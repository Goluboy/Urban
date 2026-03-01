using NetTopologySuite.Geometries;

namespace Urban.Application.Helpers
{
    public static class GeometryUtils
    {
        private static readonly GeometryFactory _geometryFactory = GeometryFactory.Default;

        public static Polygon CreateRectangle(double angle, Coordinate center, double left, double right, double down, double up)
        {
            var localCoords = new[] 
            { 
                new Coordinate(-left, -down), 
                new Coordinate(right, -down), 
                new Coordinate(right, up), 
                new Coordinate(-left, up), 
                new Coordinate(-left, -down) 
            };
            
            var rotatedCoords = Rotate(localCoords, angle, center.X, center.Y);
            return _geometryFactory.CreatePolygon(rotatedCoords);
        }

        public static Polygon CreateSquare(double angle, Coordinate center, double size)
        {
            return CreateRectangle(angle, center, size, size, size, size);
        }

        public static Coordinate[] Rotate(Coordinate[] localCoords, double angle, double centerX, double centerY)
        {
            Coordinate[] rotated = new Coordinate[localCoords.Length];
            
            for (int i = 0; i < localCoords.Length; i++)
            {
                double x = localCoords[i].X;
                double y = localCoords[i].Y;
                double rotatedX = x * Math.Cos(angle) - y * Math.Sin(angle);
                double rotatedY = x * Math.Sin(angle) + y * Math.Cos(angle);
                rotated[i] = new Coordinate(rotatedX + centerX, rotatedY + centerY);
            }
            
            return rotated;
        }

        public static double CalculateStepSize(Polygon polygon, double precisionFactor = 0.01)
        {
            double maxSize = Math.Min(polygon.EnvelopeInternal.Width, polygon.EnvelopeInternal.Height);
            return maxSize * precisionFactor;
        }

        public static double CalculateCoarseStepSize(Polygon polygon)
        {
            return CalculateStepSize(polygon, 0.01);
        }

        public static double CalculateFineStepSize(Polygon polygon)
        {
            return CalculateStepSize(polygon, 0.01);
        }

        public static double FindMaxExtensionInDirection(Polygon block, double angle, Coordinate center, 
            double left, double right, double down, double up, int dirX, int dirY)
        {
            double step = CalculateFineStepSize(block);
            double maxSize = Math.Min(block.EnvelopeInternal.Width, block.EnvelopeInternal.Height);
            double maxExtension = 0;
            
            double currentExtension = 0;
            if (dirX == -1) currentExtension = left;
            else if (dirX == 1) currentExtension = right;
            else if (dirY == -1) currentExtension = down;
            else if (dirY == 1) currentExtension = up;
            
            for (double extension = currentExtension + step; extension <= maxSize; extension += step)
            {
                double testLeft = left, testRight = right, testDown = down, testUp = up;
                if (dirX == -1) testLeft = extension;
                else if (dirX == 1) testRight = extension;
                else if (dirY == -1) testDown = extension;
                else if (dirY == 1) testUp = extension;
                
                var testRectangle = CreateRectangle(angle, center, testLeft, testRight, testDown, testUp);
                
                if (testRectangle != null && block.Contains(testRectangle))
                {
                    maxExtension = extension;
                }
                else
                {
                    break;
                }
            }
            
            return maxExtension;
        }

        public static bool IsValidContainedRectangle(Polygon rectangle, Polygon block)
        {
            return rectangle != null && block.Contains(rectangle) && rectangle.Area > 0;
        }

        public static bool AreRectanglesAtDistance(Polygon rect1, Polygon rect2, double minDistance)
        {
            return rect1.Distance(rect2) >= minDistance;
        }

        public static bool IsRectangleValidForPlacement(Polygon rectangle, Polygon plot, List<Polygon> existingRectangles, double minDistance)
        {
            if (!IsValidContainedRectangle(rectangle, plot))
                return false;
                
            foreach (var existing in existingRectangles)
            {
                if (!AreRectanglesAtDistance(rectangle, existing, minDistance))
                    return false;
            }
            
            return true;
        }

        /// <summary>
        /// Checks if a point lies on the boundary of a polygon within a specified tolerance.
        /// </summary>
        /// <param name="point">The point to check</param>
        /// <param name="polygon">The polygon to check against</param>
        /// <param name="tolerance">The tolerance for checking if the point is on the boundary (default: 1e-9)</param>
        /// <returns>True if the point lies on the polygon boundary within tolerance</returns>
        public static bool IsPointOnPolygonBoundary(Point point, Polygon polygon, double tolerance = 1e-9)
        {
            if (point == null || polygon == null)
                return false;

            // Check if the distance from point to polygon boundary is within tolerance
            return polygon.Boundary.Distance(point) <= tolerance;
        }

        /// <summary>
        /// Checks if a coordinate lies on the boundary of a polygon within a specified tolerance.
        /// </summary>
        /// <param name="coordinate">The coordinate to check</param>
        /// <param name="polygon">The polygon to check against</param>
        /// <param name="tolerance">The tolerance for checking if the coordinate is on the boundary (default: 1e-9)</param>
        /// <returns>True if the coordinate lies on the polygon boundary within tolerance</returns>
        public static bool IsCoordinateOnPolygonBoundary(Coordinate coordinate, Polygon polygon, double tolerance = 1e-9)
        {
            if (coordinate == null || polygon == null)
                return false;

            var point = _geometryFactory.CreatePoint(coordinate);
            return IsPointOnPolygonBoundary(point, polygon, tolerance);
        }

        /// <summary>
        /// Calculates the interior angles of a polygon in degrees.
        /// For a well-formed rectangle, all angles should be close to 90 degrees.
        /// </summary>
        /// <param name="polygon">The polygon to analyze</param>
        /// <returns>Array of interior angles in degrees</returns>
        public static double[] GetPolygonAngles(this Polygon polygon)
        {
            if (polygon == null || polygon.IsEmpty)
                throw new ArgumentException("Polygon cannot be null or empty");

            var coordinates = polygon.ExteriorRing.Coordinates;
            if (coordinates.Length < 4) // Need at least 3 points + closing point
                throw new ArgumentException("Polygon must have at least 3 vertices");

            var angles = new List<double>();
            int numVertices = coordinates.Length - 1; // Exclude the closing coordinate

            // First, determine if polygon is clockwise or counter-clockwise
            bool isClockwise = IsPolygonClockwise(coordinates);

            for (int i = 0; i < numVertices; i++)
            {
                // Get three consecutive points
                var p1 = coordinates[i];
                var p2 = coordinates[(i + 1) % numVertices];
                var p3 = coordinates[(i + 2) % numVertices];

                // Calculate vectors from p2 to p1 and p2 to p3
                var v1 = new { X = p1.X - p2.X, Y = p1.Y - p2.Y };
                var v2 = new { X = p3.X - p2.X, Y = p3.Y - p2.Y };

                // Calculate cross product (for determining turn direction)
                var cross = v1.X * v2.Y - v1.Y * v2.X;
                
                // Calculate dot product
                var dot = v1.X * v2.X + v1.Y * v2.Y;
                
                // Calculate angle using atan2 for full 360° range
                var angleRadians = Math.Atan2(cross, dot);
                
                // Convert to positive angle
                if (angleRadians < 0)
                    angleRadians += 2 * Math.PI;
                
                // For interior angles, we need to consider polygon orientation
                if (!isClockwise)
                {
                    angleRadians = 2 * Math.PI - angleRadians;
                }
                
                var angleDegrees = angleRadians * 180.0 / Math.PI;
                angles.Add(angleDegrees);
            }

            return angles.ToArray();
        }

        /// <summary>
        /// Determines if a polygon is oriented clockwise or counter-clockwise using the shoelace formula.
        /// </summary>
        /// <param name="coordinates">The coordinates of the polygon vertices</param>
        /// <returns>True if the polygon is clockwise, false if counter-clockwise</returns>
        public static bool IsPolygonClockwise(Coordinate[] coordinates)
        {
            // Calculate the signed area using the shoelace formula
            double signedArea = 0.0;
            int n = coordinates.Length - 1; // Exclude the duplicate last point
            
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                signedArea += (coordinates[j].X - coordinates[i].X) * (coordinates[j].Y + coordinates[i].Y);
            }
            
            // If signed area is positive, polygon is clockwise
            // If signed area is negative, polygon is counter-clockwise
            return signedArea > 0;
        }

        public static double AngleBetween(Coordinate a, Coordinate b)
        {
            var dot = a.X * b.X + a.Y * b.Y;
            var magA = Math.Sqrt(a.X * a.X + a.Y * a.Y);
            var magB = Math.Sqrt(b.X * b.X + b.Y * b.Y);
            return Math.Acos(Math.Clamp(dot / (magA * magB), -1, 1));
        }

        /// <summary>
        /// Normalizes a vector (coordinate) to unit length.
        /// </summary>
        public static Coordinate Normalize(Coordinate vector)
        {
            var length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
            return length > 0 ? new Coordinate(vector.X / length, vector.Y / length) : new Coordinate(0, 0);
        }

        /// <summary>
        /// Calculates the distance between two coordinates.
        /// </summary>
        public static double Distance(Coordinate a, Coordinate b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Interpolates a point along a line segment at parameter t (0 to 1).
        /// </summary>
        public static Coordinate InterpolateOnEdge(Coordinate start, Coordinate end, double t)
        {
            return new Coordinate(
                start.X + t * (end.X - start.X),
                start.Y + t * (end.Y - start.Y)
            );
        }

        /// <summary>
        /// Interpolates a point along a line segment at specified distance from start.
        /// </summary>
        public static Coordinate InterpolateOnEdgeByDistance(Coordinate start, Coordinate end, double distance)
        {
            var edgeLength = Distance(start, end);
            if (edgeLength == 0) return start;
            var t = distance / edgeLength;
            return InterpolateOnEdge(start, end, t);
        }

        /// <summary>
        /// Gets the direction vector from start to end, normalized.
        /// </summary>
        public static Coordinate GetDirection(Coordinate start, Coordinate end)
        {
            return Normalize(new Coordinate(end.X - start.X, end.Y - start.Y));
        }

        /// <summary>
        /// Gets a perpendicular vector to the given direction (rotated 90 degrees counterclockwise).
        /// </summary>
        public static Coordinate GetPerpendicularDirection(Coordinate direction)
        {
            return new Coordinate(-direction.Y, direction.X);
        }

        /// <summary>
        /// Finds the intersection point of two infinite lines defined by their start and end points.
        /// Returns null if lines are parallel.
        /// </summary>
        public static Coordinate? FindLineIntersection(Coordinate line1Start, Coordinate line1End, Coordinate line2Start, Coordinate line2End)
        {
            var d1 = new Coordinate(line1End.X - line1Start.X, line1End.Y - line1Start.Y);
            var d2 = new Coordinate(line2End.X - line2Start.X, line2End.Y - line2Start.Y);
            
            var denominator = d1.X * d2.Y - d1.Y * d2.X;
            if (Math.Abs(denominator) < 1e-10) return null;
            
            var dx = line2Start.X - line1Start.X;
            var dy = line2Start.Y - line1Start.Y;
            var t = (dx * d2.Y - dy * d2.X) / denominator;
            
            return new Coordinate(line1Start.X + t * d1.X, line1Start.Y + t * d1.Y);
        }

        /// <summary>
        /// Tests if a direction vector points inward relative to a polygon at a given point.
        /// </summary>
        public static bool IsDirectionInward(Polygon polygon, Coordinate point, Coordinate direction, double testDistance = 1.0)
        {
            var testPoint = new Coordinate(point.X + direction.X * testDistance, point.Y + direction.Y * testDistance);
            return polygon.Contains(new Point(testPoint));
        }

        /// <summary>
        /// Checks if two polygons have significant intersection (area > threshold).
        /// </summary>
        public static bool HasSignificantIntersection(Polygon poly1, Polygon poly2, double areaThreshold = 0.1)
        {
            try
            {
                return poly1.Intersects(poly2) && poly1.Intersection(poly2).Area > areaThreshold;
            }
            catch (Exception e)
            {
                Console.WriteLine(poly1);
                Console.WriteLine(poly2);
                throw;
            }
            
            // return poly1.Relate(poly2, "2********");
            // return poly1.Intersects(poly2) && !poly1.Touches(poly2);
            
        }
    }
} 