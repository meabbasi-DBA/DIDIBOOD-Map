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
    public CoveragePolygonGeometryDto Geometry { get; init; } = new();
}

public sealed class CoverageFeaturePropertiesDto
{
    public long H3Index { get; init; }
    public string Status { get; init; } = "pending";
    public int PoiCount { get; init; }
    public DateTimeOffset? LastCrawlAt { get; init; }
    public DateTimeOffset? LastSuccessAt { get; init; }
    public double CentroidLat { get; init; }
    public double CentroidLng { get; init; }
}

public sealed class CoveragePolygonGeometryDto
{
    public string Type { get; init; } = "Polygon";
    public IReadOnlyList<IReadOnlyList<double[]>> Coordinates { get; init; } = [];
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
