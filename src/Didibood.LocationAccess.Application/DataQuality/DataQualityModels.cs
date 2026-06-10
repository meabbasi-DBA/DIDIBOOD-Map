namespace Didibood.LocationAccess.Application.DataQuality;

public sealed class DataQualityCompareRequest
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string SearchTerm { get; init; } = string.Empty;
    public int RadiusMeters { get; init; } = 2000;
}

public sealed class DataQualityCompareResult
{
    public int LiveCount { get; init; }
    public int DbCount { get; init; }
    public int MatchedCount { get; init; }
    public int LiveOnlyCount { get; init; }
    public int DbOnlyCount { get; init; }
    public IReadOnlyList<DataQualityPoiRowDto> LiveResults { get; init; } = [];
    public IReadOnlyList<DataQualityPoiRowDto> DbResults { get; init; } = [];
    public IReadOnlyList<DataQualityMismatchDto> Mismatches { get; init; } = [];
}

public sealed class DataQualityPoiRowDto
{
    public Guid? Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Address { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string? Fingerprint { get; init; }
    public string Source { get; init; } = string.Empty;
}

public sealed class DataQualityMismatchDto
{
    public string Kind { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Address { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string? Fingerprint { get; init; }
}

public sealed class DataQualityPoiDetailDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Address { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Fingerprint { get; init; } = string.Empty;
    public string CategoryCode { get; init; } = string.Empty;
    public string SourcePayloadJson { get; init; } = "{}";
    public DateTimeOffset FirstSeenAt { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
}
