using NetTopologySuite.Geometries;

namespace Urban.Domain.Geometry;

public class Bay
{
    public Coordinate Start;
    public Coordinate End;
    public List<double> ShadowHeights = [];
    public double ResultShadowHeight;
    public int Floors;
    public int EdgeIndex;
    public double Height;
}