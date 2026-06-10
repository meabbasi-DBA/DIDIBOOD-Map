namespace Didibood.LocationAccess.Domain.Entities;

public class CrawlJobExecution
{
    public Guid Id { get; set; }
    public Guid CrawlJobId { get; set; }
    public string Status { get; set; } = "running";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public long? DurationMs { get; set; }
    public int RequestCount { get; set; }
    public int NewRecords { get; set; }
    public int UpdatedRecords { get; set; }
    public int FailedRecords { get; set; }
    public int CellsProcessed { get; set; }
    public int CellsFailed { get; set; }
    public string? ErrorSummary { get; set; }
    public string TriggeredBy { get; set; } = "scheduler";

    public CrawlJob CrawlJob { get; set; } = null!;
}
