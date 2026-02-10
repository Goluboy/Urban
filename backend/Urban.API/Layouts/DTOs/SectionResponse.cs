namespace Urban.API.Layouts.DTOs;

public record SectionResponse
{
    public double[][][] polygon { get; set; }
    public int floors { get; set; }
    public double height { get; set; }
    public double appartmetsArea { get; set; }
    public double commercialArea { get; set; }
    public int parkingPlaces { get; set; }
    public int residents { get; set; }
    public int kindergardenPlaces { get; set; }
    public int schoolPlaces { get; set; }
}