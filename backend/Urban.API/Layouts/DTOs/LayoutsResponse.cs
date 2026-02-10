namespace Urban.API.Layouts.DTOs;

public record LayoutsResponse
{
    public LayoutResponse[] layouts { get; set; }
}