using NetTopologySuite.Geometries;

namespace Urban.Application.Services
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

	public class Bay
	{	
		public Coordinate Start;
		public Coordinate End;
		public List<double> ShadowHeights = new();
		public double ResultShadowHeigh;
		public int Floors;
		public int EdgeIndex;
		public double Height;
	}

	public class Restriction
	{
		public Geometry Geometry { get; set; }
		public string Name { get; set; }
		public string RestrictionType { get; set; }
	}
	
}
