namespace Didibood.LocationAccess.Domain.Entities;

public class H3CoverageCell
{
    public long H3Index { get; set; }
    public int? GridNumber { get; set; }
    public short Resolution { get; set; }
    public long? ParentH3Index { get; set; }
    public bool IsRefined { get; set; }
    public bool MunicipalityMode { get; set; }
    public string Status { get; set; } = "pending";
    public double CentroidLatitude { get; set; }
    public double CentroidLongitude { get; set; }
    public DateTimeOffset? LastCrawlAt { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public int PoiCount { get; set; }
    public int RequestCount { get; set; }
    public int FailureCount { get; set; }
    public string? FailureReason { get; set; }
    public int CrawlAttemptCount { get; set; }
    public int CrawlSuccessCount { get; set; }
    public string? LastCrawlStatus { get; set; }
    public long? LastCrawlDurationMs { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? NextEligibleCrawlAt { get; set; }
    public double CoverageScore { get; set; }
    public string? CrawlLockOwner { get; set; }
    public DateTimeOffset? CrawlLockExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
