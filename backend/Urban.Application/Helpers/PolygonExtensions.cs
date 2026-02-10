using NetTopologySuite.Geometries;

namespace Urban.Application.Helpers
{
    public static class GeometryExtensions
    {
        public static LineSegment[] GetEdges(this Polygon polygon) => GetEdges(polygon.ExteriorRing.Coordinates);
        
        public static LineString[] GetEdgesAsLineStrings(this Polygon polygon) => GetEdgesAsLineStrings(polygon.ExteriorRing.Coordinates);

        public static LineSegment[] GetEdges(this LineString line) => GetEdges(line.Coordinates);
        
        private static LineString[] GetEdgesAsLineStrings(Coordinate[] coords) => coords.Zip(coords.Skip(1), (c1, c2) => new LineString(new [] {c1, c2})).ToArray();

        private static LineSegment[] GetEdges(Coordinate[] coords) => coords.Zip(coords.Skip(1), (c1, c2) => new LineSegment(c1, c2)).ToArray();

        public static Coordinate GetInwardPerpVector(this Polygon polygon, Point entryPoint)
        {            
            var edge = polygon.GetEdges() .OrderBy(edge => edge.Distance(entryPoint.Coordinate)).First();
            var dx = edge.P1.X - edge.P0.X;
            var dy = edge.P1.Y - edge.P0.Y;
            var perp = new Coordinate(-dy, dx);
            var test = new Coordinate(entryPoint.X + perp.X, entryPoint.Y + perp.Y);
            return polygon.Contains(new Point(test)) ? perp : new Coordinate(dy, -dx);                
        }
    }
}


