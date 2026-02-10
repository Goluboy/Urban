namespace Urban.API.Layouts.DTOs;

public record ErrorResponse
{
    public string error { get; set; }
    public string details { get; set; }
}