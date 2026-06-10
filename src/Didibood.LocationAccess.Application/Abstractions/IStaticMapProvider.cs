using Didibood.LocationAccess.Domain.Entities;

namespace Didibood.LocationAccess.Application.Abstractions;

public interface IStaticMapProvider
{
    Task<StaticMapResult> GetMapAsync(StaticMapRequest request, CancellationToken ct);
    Task<IReadOnlyList<StaticMapSnapshot>> GetCachedSnapshotsAsync(CancellationToken ct);
    Task DeleteCachedSnapshotAsync(Guid id, CancellationToken ct);
}

public sealed class StaticMapRequest
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int Zoom { get; init; } = 14;
    public int Width { get; init; } = 600;
    public int Height { get; init; } = 400;
    public string? Style { get; init; }
    public string? Marker { get; init; }
}

public sealed class StaticMapResult
{
    public byte[]? ImageData { get; init; }
    public string? CacheKey { get; init; }
    public bool FromCache { get; init; }
}
