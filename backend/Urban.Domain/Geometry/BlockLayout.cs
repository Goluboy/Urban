using NetTopologySuite.Geometries;

namespace Urban.Domain.Geometry
{
    public class BlockLayout
    {
        public string Name;
        public Polygon Block;
        public Section[] Sections;
        public double Insolation;
        public double Cost;
        public double BuiltUpArea;
        public double UsefulArea;
        public double Value;
        public LineString[] Streets;
        public Polygon[] Parks;
    }
}