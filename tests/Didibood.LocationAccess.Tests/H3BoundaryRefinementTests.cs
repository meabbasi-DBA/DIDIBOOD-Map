using Didibood.LocationAccess.Infrastructure.H3;



namespace Didibood.LocationAccess.Tests;



public class H3BoundaryRefinementTests

{

    private static TehranMunicipalityBoundary LoadBoundary()

    {

        var path = Path.Combine(AppContext.BaseDirectory, "data", "tehran-areas-22.geojson");

        return TehranMunicipalityBoundary.LoadFromFile(path);

    }



    [Fact]

    public void PlanTehranMunicipalityGrid_WithRefinement_StaysWithinBudget()

    {

        var boundary = LoadBoundary();

        var plan = H3GridPlanner.PlanTehranMunicipalityGrid(boundary, enableBoundaryRefinement: true);



        Assert.Equal(7, plan.StorageResolution);

        Assert.Equal(114, plan.BaseCellCount);

        Assert.True(plan.VirtualCenterCount > 0);

        Assert.True(plan.SearchCenters.Count == plan.BaseCellCount + plan.VirtualCenterCount);

        Assert.True(plan.EstimatedRequestsPerCrawl <= H3GridPlanner.MaxCrawlRequests);



        var increase = (plan.EstimatedRequestsPerCrawl / (double)(plan.BaseCellCount * H3GridPlanner.DefaultSearchTermsPerCrawl)) - 1;

        Assert.True(increase <= H3BoundaryRefinementPlanner.MaxRequestIncreaseRatio + 0.01);

    }



    [Fact]

    public void BoundaryRefinement_ImprovesEstimatedCoverage()

    {

        var boundary = LoadBoundary();

        var basePlan = H3GridPlanner.PlanTehranMunicipalityGrid(boundary, enableBoundaryRefinement: false);

        var refinedPlan = H3GridPlanner.PlanTehranMunicipalityGrid(boundary, enableBoundaryRefinement: true);



        Assert.True(refinedPlan.SpatialMetrics!.EstimatedCoveragePercent

                    >= basePlan.SpatialMetrics!.EstimatedCoveragePercent);

        Assert.True(refinedPlan.SpatialMetrics.EstimatedCoveragePercent >= 90);

    }



    [Fact]

    public void ScenarioComparison_ShowsRefinementBeatsFullRes8OnRequestEfficiency()

    {

        var boundary = LoadBoundary();

        var scenarios = H3CoverageMetrics.CompareScenarios(boundary, H3GridPlanner.DefaultSearchTermsPerCrawl);



        var baseScenario = scenarios.Single(s => s.Scenario == "A_res7_base");

        var refinedScenario = scenarios.Single(s => s.Scenario == "B_res7_boundary_refinement");

        var res8Scenario = scenarios.Single(s => s.Scenario == "C_res8_full_polyfill");



        Assert.True(refinedScenario.EstimatedRequests < res8Scenario.EstimatedRequests);

        Assert.True(refinedScenario.EstimatedCoveragePercent >= baseScenario.EstimatedCoveragePercent);

        Assert.True(refinedScenario.SearchCenterCount < res8Scenario.SearchCenterCount);

    }



    [Fact]

    public void VirtualSubCentroids_AreInsideMunicipality()

    {

        var boundary = LoadBoundary();

        var baseCells = H3CellGeometry.PolyfillMunicipality(boundary, 7);

        var centers = H3BoundaryRefinementPlanner.PlanSearchCenters(

            baseCells, boundary, 2000, H3GridPlanner.DefaultSearchTermsPerCrawl);



        foreach (var center in centers.Where(c => c.IsRefined))

        {

            Assert.True(boundary.ContainsPoint(center.Lat, center.Lng));

            Assert.NotNull(center.ParentH3Index);

            Assert.True(H3VirtualCenterId.IsVirtual(center.H3Index));

        }

    }



    [Fact]

    public void RecommendSearchRadius_IsDerivedFromH3Spacing()

    {

        var boundary = LoadBoundary();

        var cells = H3CellGeometry.PolyfillMunicipality(boundary, 7);

        var spacing = H3CoverageMetrics.ComputeCenterSpacing(cells.Select(H3CellGeometry.GetCentroid).ToList());



        Assert.InRange(spacing.Median, 1900, 2800);

        var recommended = H3CoverageMetrics.RecommendSearchRadius(spacing.Median, 2000);
        Assert.InRange(recommended, 1850, 2150);

    }



    [Fact]

    public void Refinement_ProducesCandidates_EvenWithHighSearchRadius()

    {

        var boundary = LoadBoundary();

        var baseCells = H3CellGeometry.PolyfillMunicipality(boundary, 7);

        var result = H3BoundaryRefinementPlanner.PlanSearchCentersWithDiagnostics(

            baseCells, boundary, searchRadiusMeters: 2500, H3GridPlanner.DefaultSearchTermsPerCrawl);



        Assert.True(result.CandidateCellCount > 0);

        Assert.True(result.SelectedRefinedCount > 0);

        Assert.Equal(result.SelectedRefinedCount, result.SearchCenters.Count(c => c.IsRefined));

    }



    [Fact]

    public void RefinementDiagnostics_ExposePlannerCounts()

    {

        var boundary = LoadBoundary();

        var result = H3BoundaryRefinementPlanner.PlanSearchCentersWithDiagnostics(

            H3CellGeometry.PolyfillMunicipality(boundary, 7),

            boundary,

            2000,

            H3GridPlanner.DefaultSearchTermsPerCrawl);



        Assert.True(result.RefinementEnabled);

        Assert.True(result.CandidateCellCount > 0);

        Assert.True(result.SelectedRefinedCount > 0);

        Assert.Equal(114, result.BaseCellCount);

    }

}


