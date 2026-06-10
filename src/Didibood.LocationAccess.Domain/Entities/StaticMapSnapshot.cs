namespace Didibood.LocationAccess.Domain.Entities;

public class StaticMapSnapshot
{
    public Guid Id { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public short Zoom { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Style { get; set; }
    public string? Marker { get; set; }
    public string? ImageUrl { get; set; }
    public string? LocalFilePath { get; set; }
    public string CacheKey { get; set; } = string.Empty;
    public byte[]? ImageData { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
