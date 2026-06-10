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
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("Neshan:ApiKey is required but was not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.LocationApiKey))
        {
            logger.LogWarning("Neshan:LocationApiKey is empty; falling back to ApiKey for Search API.");
        }

        logger.LogInformation("Neshan configuration validated.");
    }

    private sealed class CountRow
    {
        public int Value { get; init; }
    }
}
