namespace Didibood.LocationAccess.Domain.Entities;

public class CrawlHistory
{
    public Guid Id { get; set; }
    public Guid? ExecutionId { get; set; }
    public string Source { get; set; } = "scheduler";
    public int? GridNumber { get; set; }
    public long H3Index { get; set; }
    public short? CategoryId { get; set; }
    public string? SearchTerm { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public long? DurationMs { get; set; }
    public string Status { get; set; } = "accepted";
    public string? Reason { get; set; }
    public int ResultCount { get; set; }
    public string? ErrorMessage { get; set; }
}
