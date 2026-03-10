namespace Urban.Application.Interfaces.Results;

public record ImportResult
{
    public int FeatureCount { get; init; }
    public long ElapsedMilliseconds { get; init; }
}