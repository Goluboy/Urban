using NetTopologySuite.Geometries;

namespace Urban.Application.Upgrades
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