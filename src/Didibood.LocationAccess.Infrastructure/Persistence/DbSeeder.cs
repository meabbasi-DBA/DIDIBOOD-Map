using Didibood.LocationAccess.Domain;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Infrastructure.H3;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Infrastructure.Persistence;

internal static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.PoiCategories.AnyAsync(cancellationToken))
        {
            db.PoiCategories.AddRange(
                new PoiCategory { Id = 1, Code = "metro", NameEn = "Metro Station", NameFa = "ایستگاه مترو", SearchTermsJson = """["ایستگاه مترو","مترو"]""", DisplayOrder = 1, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 2, Code = "brt", NameEn = "BRT Station", NameFa = "ایستگاه BRT", SearchTermsJson = """["ایستگاه BRT","اتوبوس تندرو"]""", DisplayOrder = 2, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 3, Code = "bus", NameEn = "Bus Stop", NameFa = "ایستگاه اتوبوس", SearchTermsJson = """["ایستگاه اتوبوس"]""", DisplayOrder = 3, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 4, Code = "school", NameEn = "School", NameFa = "مدرسه", SearchTermsJson = """["مدرسه","دبستان","دبیرستان"]""", DisplayOrder = 4, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 5, Code = "university", NameEn = "University", NameFa = "دانشگاه", SearchTermsJson = """["دانشگاه","دانشکده"]""", DisplayOrder = 5, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 6, Code = "hospital", NameEn = "Hospital", NameFa = "بیمارستان", SearchTermsJson = """["بیمارستان"]""", DisplayOrder = 6, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 7, Code = "clinic", NameEn = "Clinic", NameFa = "درمانگاه", SearchTermsJson = """["درمانگاه","کلینیک"]""", DisplayOrder = 7, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 8, Code = "pharmacy", NameEn = "Pharmacy", NameFa = "داروخانه", SearchTermsJson = """["داروخانه"]""", DisplayOrder = 8, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 9, Code = "shoppingCenter", NameEn = "Shopping Center", NameFa = "مرکز خرید", SearchTermsJson = """["مرکز خرید","مجتمع تجاری"]""", DisplayOrder = 9, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 10, Code = "supermarket", NameEn = "Supermarket", NameFa = "سوپرمارکت", SearchTermsJson = """["سوپرمارکت","هایپرمارکت"]""", DisplayOrder = 10, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 11, Code = "park", NameEn = "Park", NameFa = "پارک", SearchTermsJson = """["پارک","بوستان"]""", DisplayOrder = 11, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 12, Code = "gym", NameEn = "Gym", NameFa = "باشگاه ورزشی", SearchTermsJson = """["باشگاه ورزشی","سالن ورزشی"]""", DisplayOrder = 12, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 13, Code = "bank", NameEn = "Bank", NameFa = "بانک", SearchTermsJson = """["بانک","شعبه بانک"]""", DisplayOrder = 13, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 14, Code = "mosque", NameEn = "Mosque", NameFa = "مسجد", SearchTermsJson = """["مسجد"]""", DisplayOrder = 14, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new PoiCategory { Id = 15, Code = "governmentOffice", NameEn = "Government Office", NameFa = "اداره دولتی", SearchTermsJson = """["اداره","دفتر پیشخوان"]""", DisplayOrder = 15, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        }

        if (!await db.SystemConfigurations.AnyAsync(cancellationToken))
        {
            var now = DateTimeOffset.UtcNow;
            db.SystemConfigurations.AddRange(
                new SystemConfiguration { ConfigKey = "search.radius.default_meters", ConfigValue = "2000", ValueType = "int", Description = "Neshan search disc radius (meters) for overlap planning", UpdatedAt = now, UpdatedBy = "system" },
                new SystemConfiguration { ConfigKey = "search.max_results_per_category", ConfigValue = "20", ValueType = "int", Description = "Max POIs per category", UpdatedAt = now, UpdatedBy = "system" },
                new SystemConfiguration { ConfigKey = "crawl.batch_size", ConfigValue = "10", ValueType = "int", UpdatedAt = now, UpdatedBy = "system" },
                new SystemConfiguration { ConfigKey = "crawl.parallelism", ConfigValue = "2", ValueType = "int", UpdatedAt = now, UpdatedBy = "system" },
                new SystemConfiguration { ConfigKey = "crawl.retry.count", ConfigValue = "3", ValueType = "int", UpdatedAt = now, UpdatedBy = "system" },
                new SystemConfiguration { ConfigKey = "crawl.retry.delay_ms", ConfigValue = "2000", ValueType = "int", UpdatedAt = now, UpdatedBy = "system" },
                new SystemConfiguration { ConfigKey = "crawl.stale_threshold_days", ConfigValue = "30", ValueType = "int", UpdatedAt = now, UpdatedBy = "system" },
                new SystemConfiguration { ConfigKey = "crawl.h3_resolution", ConfigValue = "auto", ValueType = "string", Description = "H3 crawl resolution: auto (coarsest overlap-safe) or 6-8", UpdatedAt = now, UpdatedBy = "system" },
                new SystemConfiguration { ConfigKey = "crawl.h3_reseed_on_startup", ConfigValue = "false", ValueType = "bool", Description = "Rebuild H3 grid when resolution/cell count differs from plan", UpdatedAt = now, UpdatedBy = "system" },
                new SystemConfiguration { ConfigKey = "tehran.boundary.mode", ConfigValue = "municipality", ValueType = "string", Description = "Grid source: municipality polygon or legacy bbox", UpdatedAt = now, UpdatedBy = "system" },
                new SystemConfiguration { ConfigKey = "tehran.bounds", ConfigValue = """{"minLat":35.50,"maxLat":35.88,"minLng":51.10,"maxLng":51.62}""", ValueType = "json", UpdatedAt = now, UpdatedBy = "system" });
        }

        await db.SaveChangesAsync(cancellationToken);

        await EnsureSystemConfigurationAsync(db, "crawl.h3_resolution", "auto", "string",
            "H3 crawl resolution: auto (coarsest overlap-safe) or 6-8", cancellationToken);
        await EnsureSystemConfigurationAsync(db, "crawl.h3_reseed_on_startup", "false", "bool",
            "Rebuild H3 grid when resolution/cell count differs from plan", cancellationToken);
        await EnsureSystemConfigurationAsync(db, H3BoundaryRefinementPlanner.RefinementConfigKey, "true", "bool",
            "Enable virtual sub-centroids inside municipality boundary H3 cells", cancellationToken);

        await H3GridSeeder.SeedTehranGridAsync(db, cancellationToken);

        if (!await db.CrawlJobs.AnyAsync(cancellationToken))
        {
            var gridResolution = await db.H3CoverageCells
                .Select(c => c.Resolution)
                .FirstOrDefaultAsync(cancellationToken);
            if (gridResolution == 0)
                gridResolution = 8;

            var now = DateTimeOffset.UtcNow;
            db.CrawlJobs.AddRange(
                new CrawlJob
                {
                    Id = Guid.NewGuid(),
                    Name = "tehran-manual",
                    Description = "On-demand Tehran crawl — starts immediately from Admin (no cron)",
                    JobType = CrawlJobKinds.Manual,
                    CronExpression = null,
                    H3Resolution = gridResolution,
                    IsEnabled = true,
                    MaxParallelCells = 2,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new CrawlJob
                {
                    Id = Guid.NewGuid(),
                    Name = "tehran-daily",
                    Description = "Full Tehran grid crawl every 24 hours",
                    JobType = CrawlJobKinds.Scheduled,
                    CronExpression = "0 2 * * *",
                    H3Resolution = gridResolution,
                    IsEnabled = false,
                    MaxParallelCells = 2,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new CrawlJob
                {
                    Id = Guid.NewGuid(),
                    Name = "tehran-stale-refresh",
                    Description = "Re-crawl stale or failed cells every 7 days",
                    JobType = CrawlJobKinds.Scheduled,
                    CronExpression = "0 3 * * 0",
                    H3Resolution = gridResolution,
                    IsEnabled = false,
                    MaxParallelCells = 2,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            await db.SaveChangesAsync(cancellationToken);
        }

        await EnsureManualCrawlJobAsync(db, cancellationToken);
    }

    private static async Task EnsureManualCrawlJobAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.CrawlJobs.AnyAsync(j => j.JobType == CrawlJobKinds.Manual, cancellationToken))
            return;

        var gridResolution = await db.H3CoverageCells
            .Select(c => c.Resolution)
            .FirstOrDefaultAsync(cancellationToken);
        if (gridResolution == 0)
            gridResolution = 8;

        var now = DateTimeOffset.UtcNow;
        db.CrawlJobs.Add(new CrawlJob
        {
            Id = Guid.NewGuid(),
            Name = "tehran-manual",
            Description = "On-demand Tehran crawl — starts immediately from Admin (no cron)",
            JobType = CrawlJobKinds.Manual,
            CronExpression = null,
            H3Resolution = gridResolution,
            IsEnabled = true,
            MaxParallelCells = 2,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureSystemConfigurationAsync(
        AppDbContext db,
        string key,
        string value,
        string valueType,
        string description,
        CancellationToken ct)
    {
        if (await db.SystemConfigurations.AnyAsync(c => c.ConfigKey == key, ct))
            return;

        db.SystemConfigurations.Add(new SystemConfiguration
        {
            ConfigKey = key,
            ConfigValue = value,
            ValueType = valueType,
            Description = description,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = "system"
        });
        await db.SaveChangesAsync(ct);
    }
}
