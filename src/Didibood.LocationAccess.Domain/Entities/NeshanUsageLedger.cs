namespace Didibood.LocationAccess.Domain.Entities;

public class NeshanUsageLedger
{
    public Guid Id { get; set; }
    public Guid? ExecutionId { get; set; }
    public string Source { get; set; } = "scheduler";
    public string Endpoint { get; set; } = "search";
    public int? GridNumber { get; set; }
    public long? H3Index { get; set; }
    public short? CategoryId { get; set; }
    public string? SearchTerm { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public bool Accepted { get; set; }
    public string Reason { get; set; } = "";
    public int CostUnits { get; set; } = 1;
    public long? DurationMs { get; set; }
    public int? HttpStatus { get; set; }
    public string? ErrorMessage { get; set; }
}
