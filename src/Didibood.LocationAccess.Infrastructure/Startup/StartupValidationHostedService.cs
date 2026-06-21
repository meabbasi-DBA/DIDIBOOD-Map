using Didibood.LocationAccess.Application.Configuration;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Didibood.LocationAccess.Infrastructure.Startup;

public sealed class StartupValidationHostedService(
    IServiceProvider serviceProvider,
    IOptions<NeshanOptions> neshanOptions,
    ILogger<StartupValidationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ValidateNeshanConfiguration();

        using var scope = serviceProvider.CreateScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<DatabaseBootstrapper>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        logger.LogInformation("Startup validation: ensuring database and extensions...");
        await bootstrapper.EnsureDatabaseAsync(cancellationToken);
        await bootstrapper.MigrateAndSeedAsync(db, cancellationToken);

        var tableCountRow = await db.Database
            .SqlQueryRaw<CountRow>("SELECT COUNT(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .FirstAsync(cancellationToken);
        var tableCount = tableCountRow.Value;

        if (tableCount < 5)
        {
            throw new InvalidOperationException("Required database tables were not created.");
        }

        logger.LogInformation("Startup validation completed successfully ({TableCount} public tables).", tableCount);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ValidateNeshanConfiguration()
    {
        var options = neshanOptions.Value;
        var searchKey = options.GetSearchApiKey();
        if (string.IsNullOrWhiteSpace(searchKey))
        {
            logger.LogWarning("Neshan:SearchApiKey is missing; Neshan Search calls will fail safely until it is configured.");
        }

        if (string.IsNullOrWhiteSpace(options.LocationApiKey))
            logger.LogWarning("Neshan:LocationApiKey is empty; location/web map calls will use configured fallback if available.");

        if (string.IsNullOrWhiteSpace(options.ReverseGeocodeApiKey))
            logger.LogWarning("Neshan:ReverseGeocodeApiKey is empty; reverse geocode calls are not enabled.");

        if (string.IsNullOrWhiteSpace(options.RoutingApiKey))
            logger.LogWarning("Neshan:RoutingApiKey is empty; routing calls are not enabled.");

        logger.LogInformation(
            "Neshan configuration validated. SearchApiKey={SearchKey}, LocationApiKey={LocationKey}, ReverseGeocodeApiKey={ReverseKey}, RoutingApiKey={RoutingKey}",
            NeshanOptions.Mask(searchKey),
            NeshanOptions.Mask(options.GetLocationApiKey()),
            NeshanOptions.Mask(options.ReverseGeocodeApiKey),
            NeshanOptions.Mask(options.RoutingApiKey));
    }

    private sealed class CountRow
    {
        public int Value { get; init; }
    }
}
