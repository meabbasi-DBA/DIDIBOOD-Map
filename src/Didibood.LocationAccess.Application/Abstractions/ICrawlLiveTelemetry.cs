namespace Didibood.LocationAccess.Application.Abstractions;

/// <summary>
/// Ephemeral crawl error messages shared across Worker and Admin (file-backed, not in DB).
/// Cleared when the execution finishes.
/// </summary>
public interface ICrawlLiveTelemetry
{
    void SetLiveError(Guid executionId, string? error);
    string? GetLiveError(Guid executionId);
    void Clear(Guid executionId);
}
