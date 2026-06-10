using Didibood.LocationAccess.Application.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Didibood.LocationAccess.Infrastructure.HealthChecks;

public sealed class NeshanSearchHealthCheck(INeshanSearchClient searchClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await searchClient.SearchAsync("restaurant", 35.6892, 51.389, cancellationToken);
            return result.Count >= 0
                ? HealthCheckResult.Healthy($"Neshan Search OK (count={result.Count}).")
                : HealthCheckResult.Degraded("Neshan Search returned unexpected payload.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Neshan Search API unreachable.", ex);
        }
    }
}
