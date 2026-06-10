namespace Didibood.LocationAccess.Application.LocationAccess;

public sealed class LocationAccessRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Radius { get; set; } = 2000;
}

public sealed class LocationAccessResponse
{
    public Dictionary<string, List<PoiResultDto>> Categories { get; set; } = new();
}

public sealed class PoiResultDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Address { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceMeters { get; set; }
}
