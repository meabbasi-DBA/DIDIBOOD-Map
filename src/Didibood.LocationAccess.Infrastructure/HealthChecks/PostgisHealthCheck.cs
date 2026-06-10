using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Didibood.LocationAccess.Infrastructure.HealthChecks;

public sealed class PostgisHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var version = await db.Database
                .SqlQueryRaw<ScalarRow>("SELECT PostGIS_Version() AS \"Value\"")
                .FirstOrDefaultAsync(cancellationToken);

            var text = version?.Value;

            return string.IsNullOrWhiteSpace(text)
                ? HealthCheckResult.Unhealthy("PostGIS version not returned.")
                : HealthCheckResult.Healthy($"PostGIS {text}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostGIS check failed.", ex);
        }
    }

    private sealed class ScalarRow
    {
        public string Value { get; init; } = string.Empty;
    }
}
