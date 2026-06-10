namespace Didibood.LocationAccess.Application.Crawler;

public sealed class NormalizedPoi
{
    public string Fingerprint { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Address { get; init; }
    public short CategoryId { get; init; }
    public string? NeshanType { get; init; }
    public string? NeshanCategory { get; init; }
    public string SourcePayloadJson { get; init; } = "{}";
}
