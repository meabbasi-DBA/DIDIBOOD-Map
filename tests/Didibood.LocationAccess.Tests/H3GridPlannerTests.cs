using Didibood.LocationAccess.Infrastructure.H3;



namespace Didibood.LocationAccess.Tests;



public class H3GridPlannerTests

{

    private const double MinLat = H3GridPlanner.DefaultMinLat;

    private const double MaxLat = H3GridPlanner.DefaultMaxLat;

    private const double MinLng = H3GridPlanner.DefaultMinLng;

    private const double MaxLng = H3GridPlanner.DefaultMaxLng;



    private const double LegacyMinLat = 35.48, LegacyMaxLat = 35.92, LegacyMinLng = 51.08, LegacyMaxLng = 51.65;



    [Fact]

    public void PlanTehranGrid_AutoSelectsResolution7_WithBudgetAndMinRadius()

    {

        var plan = H3GridPlanner.PlanTehranGrid(MinLat, MaxLat, MinLng, MaxLng);



        Assert.Equal(7, plan.StorageResolution);

        Assert.Equal(373, plan.CellIndexes.Count);

        Assert.Equal(2100, plan.SearchRadiusMeters);

        Assert.InRange(plan.EstimatedRequestsPerCrawl, H3GridPlanner.MinCrawlRequests, H3GridPlanner.MaxCrawlRequests);

        Assert.True(H3GridPlanner.ValidateOverlap(

            plan.CellIndexes, plan.SearchRadiusMeters,

            MinLat, MaxLat, MinLng, MaxLng, samplePoints: 500));

    }



    [Fact]

    public void PlanTehranGrid_LegacyBounds_Resolution7_InBudgetAtHigherRadius()

    {

        var plan = H3GridPlanner.PlanTehranGrid(LegacyMinLat, LegacyMaxLat, LegacyMinLng, LegacyMaxLng);



        Assert.Equal(7, plan.StorageResolution);

        Assert.Equal(473, plan.CellIndexes.Count);

        Assert.Equal(2650, plan.SearchRadiusMeters);

        Assert.InRange(plan.EstimatedRequestsPerCrawl, H3GridPlanner.MinCrawlRequests, H3GridPlanner.MaxCrawlRequests);

    }



    [Fact]

    public void PlanTehranGrid_Resolution7_FailsCornerOverlapAt2Km_OnLegacyBounds()

    {

        var res7 = H3CellGeometry.PolyfillBounds(LegacyMinLat, LegacyMaxLat, LegacyMinLng, LegacyMaxLng, 7);

        Assert.Equal(473, res7.Count);

        Assert.False(H3GridPlanner.ValidateOverlap(

            res7, searchRadiusMeters: 2000,

            LegacyMinLat, LegacyMaxLat, LegacyMinLng, LegacyMaxLng));

    }



    [Fact]

    public void PlanTehranGrid_FixedResolution8_ReturnsFullPolyfillWithOptimizedRadius()

    {

        var plan = H3GridPlanner.PlanTehranGrid(

            MinLat, MaxLat, MinLng, MaxLng,

            searchRadiusMeters: 2000,

            fixedResolution: 8);



        Assert.Equal(8, plan.StorageResolution);

        Assert.InRange(plan.CellIndexes.Count, 2500, 2700);

        Assert.True(plan.SearchRadiusMeters >= 1500);

        Assert.True(H3GridPlanner.ValidateOverlap(

            plan.CellIndexes, plan.SearchRadiusMeters,

            MinLat, MaxLat, MinLng, MaxLng, samplePoints: 500));

    }



    [Fact]

    public void FindMinimumOverlapRadius_ReturnsSmallestPassingRadius()

    {

        var cells = H3CellGeometry.PolyfillBounds(MinLat, MaxLat, MinLng, MaxLng, 7);



        var minRadius = H3GridPlanner.FindMinimumOverlapRadius(cells, MinLat, MaxLat, MinLng, MaxLng);



        Assert.Equal(2100, minRadius);

        Assert.False(H3GridPlanner.ValidateOverlap(cells, minRadius!.Value - 50, MinLat, MaxLat, MinLng, MaxLng, samplePoints: 200));

    }



    [Fact]

    public void PlanTehranGrid_Resolution7_HasFewerCellsThanResolution8()

    {

        var res7 = H3CellGeometry.PolyfillBounds(MinLat, MaxLat, MinLng, MaxLng, 7).Count;

        var res8 = H3CellGeometry.PolyfillBounds(MinLat, MaxLat, MinLng, MaxLng, 8).Count;

        Assert.True(res7 < res8);

        Assert.InRange(res7, 350, 400);

        Assert.True(res8 > 2500);

    }

}

