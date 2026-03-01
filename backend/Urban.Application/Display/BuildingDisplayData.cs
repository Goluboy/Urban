using NetTopologySuite.Geometries;

namespace Urban.Application.Display
{
    public class BuildingDisplayData
    {
        public Polygon Polygon { get; set; }
        public int Floors { get; set; }

        public BuildingDisplayData() { }

        public BuildingDisplayData(Polygon polygon, int floors)
        {
            Polygon = polygon;
            Floors = floors;
        }
    }
}