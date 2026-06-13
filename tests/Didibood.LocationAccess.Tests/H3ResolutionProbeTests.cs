using Didibood.LocationAccess.Infrastructure.H3;

namespace Didibood.LocationAccess.Tests;

public class H3ResolutionProbeTests
{
    private const double MinLat = 35.48, MaxLat = 35.92, MinLng = 51.08, MaxLng = 51.65;

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Tehran_CellCount_ByResolution(int resolution)
    {
        var cells = H3CellGeometry.PolyfillBounds(MinLat, MaxLat, MinLng, MaxLng, resolution);
        Console.WriteLine($"Resolution {resolution}: {cells.Count} cells");
        Assert.True(cells.Count > 0);
    }
}
