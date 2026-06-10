using Didibood.LocationAccess.Infrastructure.H3;

namespace Didibood.LocationAccess.Tests;

public class H3CellGeometryTests
{
    [Fact]
    public void PolyfillBounds_Tehran_ReturnsManyCells()
    {
        var cells = H3CellGeometry.PolyfillBounds(35.48, 35.92, 51.08, 51.65, 8);
        Assert.NotEmpty(cells);
        Assert.True(cells.Count > 100);
        Assert.True(cells.Count < 5000);
    }

    [Fact]
    public void GetCentroid_AndBoundary_AreConsistent()
    {
        var cells = H3CellGeometry.PolyfillBounds(35.68, 35.70, 51.38, 51.40, 8);
        var h3 = cells[0];

        var (lat, lng) = H3CellGeometry.GetCentroid(h3);
        var boundary = H3CellGeometry.GetBoundary(h3);

        Assert.InRange(lat, 35.0, 36.5);
        Assert.InRange(lng, 51.0, 52.0);
        Assert.True(boundary.Count >= 4);
    }
}
