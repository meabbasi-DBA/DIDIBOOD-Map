using Didibood.LocationAccess.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;

namespace Didibood.LocationAccess.Infrastructure.Persistence.Configurations;

public sealed class H3CoverageCellConfiguration : IEntityTypeConfiguration<H3CoverageCell>
{
    public void Configure(EntityTypeBuilder<H3CoverageCell> builder)
    {
        builder.ToTable("h3_coverage_cells");
        builder.HasKey(x => x.H3Index);
        builder.Property(x => x.H3Index).HasColumnName("h3_index");
        builder.Property(x => x.Resolution).HasColumnName("resolution");
        builder.Property(x => x.ParentH3Index).HasColumnName("parent_h3_index");
        builder.Property(x => x.IsRefined).HasColumnName("is_refined");
        builder.Property(x => x.MunicipalityMode).HasColumnName("municipality_mode");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
        builder.Property<Point>("Centroid")
            .HasColumnName("centroid")
            .HasColumnType("geography (point,4326)")
            .IsRequired();
        builder.Ignore(x => x.CentroidLatitude);
        builder.Ignore(x => x.CentroidLongitude);
        builder.Property(x => x.LastCrawlAt).HasColumnName("last_crawl_at");
        builder.Property(x => x.LastSuccessAt).HasColumnName("last_success_at");
        builder.Property(x => x.PoiCount).HasColumnName("poi_count");
        builder.Property(x => x.RequestCount).HasColumnName("request_count");
        builder.Property(x => x.FailureCount).HasColumnName("failure_count");
        builder.Property(x => x.FailureReason).HasColumnName("failure_reason");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

public sealed class CrawlJobConfiguration : IEntityTypeConfiguration<CrawlJob>
{
    public void Configure(EntityTypeBuilder<CrawlJob> builder)
    {
        builder.ToTable("crawl_jobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.JobType).HasColumnName("job_type").HasMaxLength(50);
        builder.Property(x => x.CronExpression).HasColumnName("cron_expression").HasMaxLength(100);
        builder.Property(x => x.H3Resolution).HasColumnName("h3_resolution");
        builder.Property(x => x.TargetCategoryId).HasColumnName("target_category_id");
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.Property(x => x.MaxParallelCells).HasColumnName("max_parallel_cells");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

public sealed class CrawlJobExecutionConfiguration : IEntityTypeConfiguration<CrawlJobExecution>
{
    public void Configure(EntityTypeBuilder<CrawlJobExecution> builder)
    {
        builder.ToTable("crawl_job_executions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CrawlJobId).HasColumnName("crawl_job_id");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.EndedAt).HasColumnName("ended_at");
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms");
        builder.Property(x => x.RequestCount).HasColumnName("request_count");
        builder.Property(x => x.NewRecords).HasColumnName("new_records");
        builder.Property(x => x.UpdatedRecords).HasColumnName("updated_records");
        builder.Property(x => x.FailedRecords).HasColumnName("failed_records");
        builder.Property(x => x.CellsProcessed).HasColumnName("cells_processed");
        builder.Property(x => x.CellsFailed).HasColumnName("cells_failed");
        builder.Property(x => x.TotalTasksPlanned).HasColumnName("total_tasks_planned");
        builder.Property(x => x.ErrorSummary).HasColumnName("error_summary");
        builder.Property(x => x.TriggeredBy).HasColumnName("triggered_by").HasMaxLength(256);
        builder.HasOne(x => x.CrawlJob).WithMany(x => x.Executions).HasForeignKey(x => x.CrawlJobId);
    }
}

public sealed class SystemConfigurationConfiguration : IEntityTypeConfiguration<SystemConfiguration>
{
    public void Configure(EntityTypeBuilder<SystemConfiguration> builder)
    {
        builder.ToTable("system_configuration");
        builder.HasKey(x => x.ConfigKey);
        builder.Property(x => x.ConfigKey).HasColumnName("config_key").HasMaxLength(100);
        builder.Property(x => x.ConfigValue).HasColumnName("config_value").IsRequired();
        builder.Property(x => x.ValueType).HasColumnName("value_type").HasMaxLength(20);
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
    }
}

public sealed class StaticMapSnapshotConfiguration : IEntityTypeConfiguration<StaticMapSnapshot>
{
    public void Configure(EntityTypeBuilder<StaticMapSnapshot> builder)
    {
        builder.ToTable("static_map_snapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Latitude).HasColumnName("latitude").HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasColumnName("longitude").HasPrecision(9, 6);
        builder.Property<Point>("Location")
            .HasColumnName("location")
            .HasColumnType("geography (point,4326)")
            .IsRequired();
        builder.Property(x => x.Zoom).HasColumnName("zoom");
        builder.Property(x => x.Width).HasColumnName("width");
        builder.Property(x => x.Height).HasColumnName("height");
        builder.Property(x => x.Style).HasColumnName("style").HasMaxLength(100);
        builder.Property(x => x.Marker).HasColumnName("marker").HasMaxLength(500);
        builder.Property(x => x.ImageUrl).HasColumnName("image_url");
        builder.Property(x => x.LocalFilePath).HasColumnName("local_file_path");
        builder.Property(x => x.CacheKey).HasColumnName("cache_key").HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.CacheKey).IsUnique();
        builder.Property(x => x.ImageData).HasColumnName("image_data").HasColumnType("bytea");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}
