namespace Didibood.LocationAccess.Domain.Entities;

public class Poi
{
    public Guid Id { get; set; }
    public string PoiFingerprint { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Address { get; set; }
    public short CategoryId { get; set; }
    public string? NeshanType { get; set; }
    public string? NeshanCategory { get; set; }
    public string SourcePayloadJson { get; set; } = "{}";
    public DateTimeOffset? SupersededAt { get; set; }
    public Guid? SupersededByPoiId { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public PoiCategory Category { get; set; } = null!;
    public Poi? SupersededByPoi { get; set; }
}
