namespace Didibood.LocationAccess.Application.Crawler;

public sealed class CrawlExecutionSummary
{
    public int TotalRequests { get; set; }
    public int NewRecords { get; set; }
    public int UpdatedRecords { get; set; }
    public int FailedRecords { get; set; }
    public int CellsProcessed { get; set; }
    public int CellsFailed { get; set; }
    public string? LastLiveError { get; set; }
}
