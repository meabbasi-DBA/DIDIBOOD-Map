namespace Didibood.LocationAccess.Infrastructure.H3;

public sealed class H3SearchCenter
{
    public required long H3Index { get; init; }
    public long? ParentH3Index { get; init; }
    public required double Lat { get; init; }
    public required double Lng { get; init; }
    public bool IsRefined { get; init; }
    public short Resolution { get; init; } = 7;
}

public static class H3VirtualCenterId
{
    /// <summary>Negative synthetic keys for virtual sub-centroids (real H3 indices are always positive).</summary>
    public static long Create(long parentH3Index, int slot) =>
        unchecked(-(long)(((ulong)parentH3Index % 10_000_000_000_000UL) * 10 + (ulong)(slot + 1)));

    public static bool IsVirtual(long h3Index) => h3Index < 0;
}
