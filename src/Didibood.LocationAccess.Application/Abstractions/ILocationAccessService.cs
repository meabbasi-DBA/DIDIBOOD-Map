using Didibood.LocationAccess.Application.LocationAccess;

namespace Didibood.LocationAccess.Application.Abstractions;

public interface ILocationAccessService
{
    Task<LocationAccessResponse> GetNearbyAsync(
        LocationAccessRequest request,
        CancellationToken cancellationToken = default);
}
