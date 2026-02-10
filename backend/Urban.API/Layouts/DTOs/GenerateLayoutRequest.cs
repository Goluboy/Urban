namespace Urban.API.Layouts.DTOs;

public record GenerateLayoutRequest
{
    public List<Point> PolygonPoints { get; set; } = [];
    public int? MaxFloors { get; set; } = null;
    public double? GrossFloorArea { get; set; } = null;

    public class Point
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

}