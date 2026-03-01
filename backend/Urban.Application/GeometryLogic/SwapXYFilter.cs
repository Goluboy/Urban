using NetTopologySuite.Geometries;

namespace Urban.Application.GeometryLogic;

public class SwapXYFilter : ICoordinateSequenceFilter
{
    public void Filter(CoordinateSequence seq, int i)
    {
        var x = seq.GetX(i);
        var y = seq.GetY(i);
        seq.SetX(i, y);
        seq.SetY(i, x);
    }

    public bool Done => false;
    public bool GeometryChanged => true;

    public static void SwapCoordinates(Geometry geom)
    {
        if (geom == null) return;

        geom.Apply(new SwapXYFilter());
    }
}