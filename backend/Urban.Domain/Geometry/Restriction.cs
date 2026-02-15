namespace Urban.Domain.Geometry;

public class Restriction
{
    public NetTopologySuite.Geometries.Geometry Geometry { get; set; }
    public string Name { get; set; }
    public string RestrictionType { get; set; }
}