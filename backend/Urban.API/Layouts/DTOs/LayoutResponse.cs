namespace Urban.API.Layouts.DTOs;

public record LayoutResponse
{
    public string name { get; set; }
    public SectionResponse[] sections { get; set; }
    public double insolation { get; set; }        
    public double builtUpArea { get; set; }
    public double usefulArea { get; set; }
    public double[][][] streets { get; set; }
    public double[][][][] parks { get; set; }
}