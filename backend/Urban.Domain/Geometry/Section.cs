using NetTopologySuite.Geometries;

namespace Urban.Domain.Geometry;

public class Section
{
    public Polygon Polygon;
    public int Floors;
    public double Height;
    public Bay[] Bays;

    public int MinFloors, MaxFloors;

    public double AppartmetsArea;
    public double CommercialArea;
    public int ParkingPlaces;
    public int Residents;
    public int KindergardenPlaces;
    public int SchoolPlaces;

    public double Cost() => Math.Round(Polygon.Area * (Floors + 2));
    public double UsefulArea() => Math.Round(Floors * Polygon.Area);

    public void UpdateProperties(bool? hasCommerce = null)
    {
        const double floorHeight = 3.2;
        Height = Floors * floorHeight;

        var area = Polygon.Area;
        bool commerce = hasCommerce ?? (CommercialArea > 0);

        AppartmetsArea = Math.Round(area * (Floors - (commerce ? 1 : 0)) * 0.8);
        CommercialArea = Math.Round(commerce ? area * 0.75 : 0);
        ParkingPlaces = (int)Math.Round(AppartmetsArea / 80);
        Residents = (int)Math.Round(AppartmetsArea / 30);
        KindergardenPlaces = Residents * 50 / 1000;
        SchoolPlaces = Residents * 115 / 1000;
        Bays = SectionGenerator.SplitPolygonToBays(Polygon, Floors);
    }
}