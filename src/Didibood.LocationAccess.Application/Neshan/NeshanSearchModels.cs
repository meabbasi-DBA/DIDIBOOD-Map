namespace Didibood.LocationAccess.Application.Neshan;

public sealed class NeshanSearchResponse
{
    public int Count { get; set; }
    public List<NeshanSearchItem> Items { get; set; } = [];
}

public sealed class NeshanSearchItem
{
    public string Title { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Neighbourhood { get; set; }
    public string? Region { get; set; }
    public string? Type { get; set; }
    public string? Category { get; set; }
    public NeshanLocation Location { get; set; } = new();
}

public sealed class NeshanLocation
{
    public double X { get; set; }
    public double Y { get; set; }
    public string? Z { get; set; }
}

public sealed class NeshanErrorResponse
{
    public string? Status { get; set; }
    public int Code { get; set; }
    public string? Message { get; set; }
}
