using Didibood.LocationAccess.Infrastructure.H3;

namespace Didibood.LocationAccess.Tests;

public class TehranMunicipalityGridTests
{
    private static TehranMunicipalityBoundary LoadBoundary()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "tehran-areas-22.geojson");
        return TehranMunicipalityBoundary.LoadFromFile(path);
    }

    [Fact]
    public void PolyfillMunicipality_Resolution7_WithinBudget()
    {
        var boundary = LoadBoundary();
        var plan = H3GridPlanner.PlanTehranMunicipalityGrid(boundary);

        Assert.Equal(7, plan.StorageResolution);
        Assert.Equal("municipality", plan.BoundaryMode);
        Assert.InRange(plan.CellIndexes.Count, 80, 200);
        Assert.InRange(plan.EstimatedRequestsPerCrawl, 2_000, H3GridPlanner.MaxCrawlRequests);
        Assert.True(plan.EstimatedRequestsPerCrawl <= H3GridPlanner.MaxCrawlRequests);

        foreach (var index in plan.CellIndexes)
            Assert.True(boundary.ContainsCentroid(index));
    }

    [Fact]
    public void MunicipalityGrid_FewerCellsThanBboxPolyfill()
    {
        var boundary = LoadBoundary();
        var municipality = H3CellGeometry.PolyfillMunicipality(boundary, 7);
        var bbox = H3CellGeometry.PolyfillBounds(
            H3GridPlanner.DefaultMinLat, H3GridPlanner.DefaultMaxLat,
            H3GridPlanner.DefaultMinLng, H3GridPlanner.DefaultMaxLng, 7);

        Assert.True(municipality.Count < bbox.Count);
    }
}
