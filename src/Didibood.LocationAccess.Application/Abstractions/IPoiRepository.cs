using Didibood.LocationAccess.Application.Crawler;

namespace Didibood.LocationAccess.Application.Abstractions;

public interface IPoiRepository
{
    /// <summary>
    /// Inserts a new POI or updates last_seen_at and source_payload for an existing one.
    /// </summary>
    Task UpsertAsync(NormalizedPoi poi, CancellationToken ct = default);

    /// <summary>
    /// Returns true when a POI with the given SHA-256 fingerprint already exists in the store.
    /// </summary>
    Task<bool> ExistsByFingerprintAsync(string fingerprint, CancellationToken ct = default);
}
