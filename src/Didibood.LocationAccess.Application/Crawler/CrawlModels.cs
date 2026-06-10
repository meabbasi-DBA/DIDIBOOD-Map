namespace Didibood.LocationAccess.Application.Crawler;

public sealed record CrawlCell(
    long H3Index,
    double Lat,
    double Lng,
    short CategoryId,
    string SearchTerm);

public sealed record CrawlPlanRequest(
    short Resolution,
    short[]? CategoryIds,
    bool StaleOnly);

public sealed record CrawlExecutionResult(
    int NewRecords,
    int UpdatedRecords,
    int FailedRecords,
    int RequestCount,
    string? Error = null);
