namespace Didibood.LocationAccess.Domain.Entities;

public class CrawlJob
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string? CronExpression { get; set; }
    public short H3Resolution { get; set; } = 8;
    public short? TargetCategoryId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int MaxParallelCells { get; set; } = 2;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public PoiCategory? TargetCategory { get; set; }
    public ICollection<CrawlJobExecution> Executions { get; set; } = [];
}
