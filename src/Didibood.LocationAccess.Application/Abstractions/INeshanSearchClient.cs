using Didibood.LocationAccess.Application.Neshan;

namespace Didibood.LocationAccess.Application.Abstractions;

public interface INeshanSearchClient
{
    Task<NeshanSearchResponse> SearchAsync(
        string term,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}
