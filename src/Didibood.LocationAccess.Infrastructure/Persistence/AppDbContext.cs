using Didibood.LocationAccess.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PoiCategory> PoiCategories => Set<PoiCategory>();
    public DbSet<Poi> Pois => Set<Poi>();
    public DbSet<H3CoverageCell> H3CoverageCells => Set<H3CoverageCell>();
    public DbSet<CrawlJob> CrawlJobs => Set<CrawlJob>();
    public DbSet<CrawlJobExecution> CrawlJobExecutions => Set<CrawlJobExecution>();
    public DbSet<CrawlHistory> CrawlHistory => Set<CrawlHistory>();
    public DbSet<NeshanUsageLedger> NeshanUsageLedger => Set<NeshanUsageLedger>();
    public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();
    public DbSet<StaticMapSnapshot> StaticMapSnapshots => Set<StaticMapSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.HasPostgresExtension("hstore");
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
