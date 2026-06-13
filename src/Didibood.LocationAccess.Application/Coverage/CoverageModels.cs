namespace Didibood.LocationAccess.Application.Coverage;

public sealed class CoverageSummaryDto
{
    public int TotalCells { get; init; }
    public int PendingCells { get; init; }
    public int SuccessCells { get; init; }
    public int FailedCells { get; init; }
    public int StaleCells { get; init; }
    public double CoveragePercent { get; init; }
}

public sealed class CoverageCellsQuery
{
    public string? Status { get; init; }
    public short? Resolution { get; init; }
    public double? MinLat { get; init; }
    public double? MaxLat { get; init; }
    public double? MinLng { get; init; }
    public double? MaxLng { get; init; }
    public int? MaxAgeDays { get; init; }
    public int Limit { get; init; } = 500;
}

public sealed class CoverageHeatmapQuery
{
    public string? Status { get; init; }
    public short? Resolution { get; init; } = 8;
    public int? MinPoiCount { get; init; }
    public int? MaxAgeDays { get; init; }
    public short? CategoryId { get; init; }
}

public sealed class CoverageGeoJsonDto
{
    public string Type { get; init; } = "FeatureCollection";
    public IReadOnlyList<CoverageFeatureDto> Features { get; init; } = [];
}

public sealed class CoverageFeatureDto
{
    public string Type { get; init; } = "Feature";
    public CoverageFeaturePropertiesDto Properties { get; init; } = new();
    public object Geometry { get; init; } = new CoveragePolygonGeometryDto();
}

public sealed class CoverageFeaturePropertiesDto
{
    public long H3Index { get; init; }
    public long? ParentH3Index { get; init; }
    public bool IsRefined { get; init; }
    public string Status { get; init; } = "pending";
    public int PoiCount { get; init; }
    public DateTimeOffset? LastCrawlAt { get; init; }
    public DateTimeOffset? LastSuccessAt { get; init; }
    public double CentroidLat { get; init; }
    public double CentroidLng { get; init; }
    public bool MunicipalityMode { get; init; }
}

public sealed class CoveragePolygonGeometryDto
{
    public string Type { get; init; } = "Polygon";
    public IReadOnlyList<IReadOnlyList<double[]>> Coordinates { get; init; } = [];
}

public sealed class CoveragePointGeometryDto
{
    public string Type { get; init; } = "Point";
    public double[] Coordinates { get; init; } = [];
}

public sealed class CoverageCellDetailDto
{
    public long H3Index { get; init; }
    public short Resolution { get; init; }
    public string Status { get; init; } = "pending";
    public int PoiCount { get; init; }
    public int FailureCount { get; init; }
    public string? FailureReason { get; init; }
    public DateTimeOffset? LastCrawlAt { get; init; }
    public DateTimeOffset? LastSuccessAt { get; init; }
    public double CentroidLat { get; init; }
    public double CentroidLng { get; init; }
}

public sealed class HeatmapPointDto
{
    public double Lat { get; init; }
    public double Lng { get; init; }
    public int Weight { get; init; }
    public long? H3Index { get; init; }
    public string? Status { get; init; }
}

public sealed class CoverageBoundaryGeoJsonDto
{
    public string Type { get; init; } = "FeatureCollection";
    public IReadOnlyList<CoverageBoundaryFeatureDto> Features { get; init; } = [];
}

public sealed class CoverageBoundaryFeatureDto
{
    public string Type { get; init; } = "Feature";
    public Dictionary<string, object> Properties { get; init; } = new();
    public CoveragePolygonGeometryDto Geometry { get; init; } = new();
}

public sealed class CoverageDebugDto
{
    public int TotalCells { get; init; }
    public int BaseCells { get; init; }
    public int VirtualCenters { get; init; }
    public short Resolution { get; init; }
    public int SearchRadiusMeters { get; init; }
    public double CityAreaKm2 { get; init; }
    public double H3AreaKm2 { get; init; }
    public double CoveragePercent { get; init; }
    public double EstimatedCoveragePercent { get; init; }
    public double AverageCenterSpacingMeters { get; init; }
    public double UncoveredAreaKm2 { get; init; }
    public double OverlapRatio { get; init; }
    public int RecommendedSearchRadiusMeters { get; init; }
    public string SourceMode { get; init; } = "";
    public int MunicipalityModeCells { get; init; }
    public int LegacyBboxCells { get; init; }
    public int CellsOutsideMunicipality { get; init; }
    public int ExpectedMunicipalityCells { get; init; }
    public int LegacyBboxPolyfillCells { get; init; }
    public bool EnvelopeLooksRectangular { get; init; }
    public CoverageCentroidBoundsDto CentroidBounds { get; init; } = new();
    public IReadOnlyList<GridScenarioComparisonDto> ScenarioComparison { get; init; } = [];
}

public sealed class CoverageRefinementDebugDto
{
    public int BaseCells { get; init; }
    public int CandidateCells { get; init; }
    public int InsertedRefinedCells { get; init; }
    public int PersistedRefinedCells { get; init; }
    public int SkippedCells { get; init; }
    public int RejectedCells { get; init; }
    public bool RefinementEnabled { get; init; }
    public string? DisabledReason { get; init; }
    public int MaxVirtualBudget { get; init; }
    public int SearchRadiusMeters { get; init; }
    public string SourceMode { get; init; } = "";
    public string CallChain { get; init; } =
        "H3GridSeeder.ReseedTehranGridAsync → H3GridSeeder.PlanGridAsync → H3GridPlanner.PlanTehranMunicipalityGrid → H3BoundaryRefinementPlanner.PlanSearchCentersWithDiagnostics → h3_coverage_cells";
}

public sealed class GridScenarioComparisonDto
{
    public required string Scenario { get; init; }
    public int CellCount { get; init; }
    public int SearchCenterCount { get; init; }
    public int EstimatedRequests { get; init; }
    public double EstimatedCoveragePercent { get; init; }
    public double OverlapPercent { get; init; }
    public int SearchRadiusMeters { get; init; }
}

public sealed class CoverageCentroidBoundsDto
{
    public double MinLat { get; init; }
    public double MaxLat { get; init; }
    public double MinLng { get; init; }
    public double MaxLng { get; init; }
}

public sealed class CrawlAnalyticsDto
{
    public int TotalCells { get; init; }
    public int CrawledCells { get; init; }
    public int RefinedCells { get; init; }
    public int TotalRequests { get; init; }
    public int BudgetMaxRequests { get; init; }
    public double CoveredAreaSqKm { get; init; }
    public double CoveragePercent { get; init; }
    public double DuplicateReductionPercent { get; init; }
    public IReadOnlyDictionary<string, int> RequestsPerCategory { get; init; } = new Dictionary<string, int>();
}
