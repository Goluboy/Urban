namespace Urban.Domain.Geometry.Data.ValueObjects;

public class RenderOptions
{
    public string FillColor { get; set; } = default!;
    public double FillOpacity { get; set; }
    public string StrokeColor { get; set; } = default!;
    public double StrokeOpacity { get; set; }
    public string StrokeWidth { get; set; } = default!;
}