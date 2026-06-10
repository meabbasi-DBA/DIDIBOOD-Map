using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Configuration;
using Didibood.LocationAccess.Infrastructure.Coverage;
using Didibood.LocationAccess.Infrastructure.Crawler;
using Didibood.LocationAccess.Infrastructure.DataQuality;
using Didibood.LocationAccess.Infrastructure.HealthChecks;
using Didibood.LocationAccess.Infrastructure.Neshan;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Didibood.LocationAccess.Infrastructure.Services;
using Didibood.LocationAccess.Infrastructure.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Didibood.LocationAccess.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool runStartupValidation = true)
    {
        services.Configure<NeshanOptions>(configuration.GetSection(NeshanOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            }));

        services.AddMemoryCache();

        services.AddScoped<DatabaseBootstrapper>();
        services.AddScoped<IPoiFingerprintService, PoiFingerprintService>();
        services.AddScoped<ILocationAccessService, LocationAccessService>();
        services.AddScoped<IPoiNormalizer, PoiNormalizer>();
        services.AddScoped<ICrawlPlanner, CrawlPlanner>();
        services.AddScoped<ICrawlExecutor, CrawlExecutor>();
        services.AddScoped<ICrawlExecutionRunner, CrawlExecutionRunner>();
        services.AddScoped<IPoiRepository, PoiRepository>();
        services.AddScoped<ICrawlJobRepository, CrawlJobRepository>();
        services.AddScoped<ISystemConfigurationStore, SystemConfigurationStore>();
        services.AddScoped<IH3CoverageRepository, H3CoverageRepository>();
        services.AddScoped<ICoverageService, CoverageService>();
        services.AddScoped<IDataQualityService, DataQualityService>();

        services.AddHttpClient<INeshanSearchClient, NeshanSearchClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddTransientHttpErrorPolicy(policy =>
                policy.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));

        services.AddHttpClient<NeshanStaticMapService>(client =>
            {
                client.BaseAddress = new Uri("https://api.neshan.org/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddTransientHttpErrorPolicy(policy =>
                policy.WaitAndRetryAsync(2, attempt => TimeSpan.FromSeconds(attempt * 2)));

        services.AddScoped<IStaticMapProvider, NeshanStaticMapService>();

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["ready", "db"])
            .AddCheck<PostgisHealthCheck>("postgis", tags: ["ready", "db"])
            .AddCheck<NeshanSearchHealthCheck>("neshan-search", tags: ["ready", "external"]);

        if (runStartupValidation)
        {
            services.AddHostedService<StartupValidationHostedService>();
        }

        return services;
    }
}
