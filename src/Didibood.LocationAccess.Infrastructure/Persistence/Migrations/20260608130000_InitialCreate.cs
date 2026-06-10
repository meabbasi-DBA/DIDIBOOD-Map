using System;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Didibood.LocationAccess.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260608130000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:postgis", ",,")
            .Annotation("Npgsql:PostgresExtension:hstore", ",,")
            .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

        migrationBuilder.CreateTable(
            name: "poi_categories",
            columns: table => new
            {
                id = table.Column<short>(type: "smallint", nullable: false),
                code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                name_fa = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                search_terms = table.Column<string>(type: "jsonb", nullable: false),
                display_order = table.Column<short>(type: "smallint", nullable: false),
                is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_poi_categories", x => x.id));

        migrationBuilder.CreateTable(
            name: "h3_coverage_cells",
            columns: table => new
            {
                h3_index = table.Column<long>(type: "bigint", nullable: false),
                resolution = table.Column<short>(type: "smallint", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                centroid = table.Column<string>(type: "geography (point,4326)", nullable: false),
                last_crawl_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_success_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                poi_count = table.Column<int>(type: "integer", nullable: false),
                failure_count = table.Column<int>(type: "integer", nullable: false),
                failure_reason = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_h3_coverage_cells", x => x.h3_index));

        migrationBuilder.CreateTable(
            name: "system_configuration",
            columns: table => new
            {
                config_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                config_value = table.Column<string>(type: "text", nullable: false),
                value_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                description = table.Column<string>(type: "text", nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_system_configuration", x => x.config_key));

        migrationBuilder.CreateTable(
            name: "static_map_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                location = table.Column<string>(type: "geography (point,4326)", nullable: false),
                zoom = table.Column<short>(type: "smallint", nullable: false),
                width = table.Column<int>(type: "integer", nullable: false),
                height = table.Column<int>(type: "integer", nullable: false),
                style = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                marker = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                image_url = table.Column<string>(type: "text", nullable: true),
                local_file_path = table.Column<string>(type: "text", nullable: true),
                cache_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_static_map_snapshots", x => x.id));

        migrationBuilder.CreateTable(
            name: "crawl_jobs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "text", nullable: true),
                job_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                cron_expression = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                h3_resolution = table.Column<short>(type: "smallint", nullable: false),
                target_category_id = table.Column<short>(type: "smallint", nullable: true),
                is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                max_parallel_cells = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_crawl_jobs", x => x.id);
                table.ForeignKey(
                    name: "fk_crawl_jobs_poi_categories_target_category_id",
                    column: x => x.target_category_id,
                    principalTable: "poi_categories",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "pois",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                poi_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                location = table.Column<string>(type: "geography (point,4326)", nullable: false),
                title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                address = table.Column<string>(type: "text", nullable: true),
                category_id = table.Column<short>(type: "smallint", nullable: false),
                neshan_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                neshan_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                source_payload = table.Column<string>(type: "jsonb", nullable: false),
                superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                superseded_by_poi_id = table.Column<Guid>(type: "uuid", nullable: true),
                first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pois", x => x.id);
                table.ForeignKey(
                    name: "fk_pois_poi_categories_category_id",
                    column: x => x.category_id,
                    principalTable: "poi_categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_pois_pois_superseded_by_poi_id",
                    column: x => x.superseded_by_poi_id,
                    principalTable: "pois",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "crawl_job_executions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                crawl_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                duration_ms = table.Column<long>(type: "bigint", nullable: true),
                request_count = table.Column<int>(type: "integer", nullable: false),
                new_records = table.Column<int>(type: "integer", nullable: false),
                updated_records = table.Column<int>(type: "integer", nullable: false),
                failed_records = table.Column<int>(type: "integer", nullable: false),
                cells_processed = table.Column<int>(type: "integer", nullable: false),
                cells_failed = table.Column<int>(type: "integer", nullable: false),
                error_summary = table.Column<string>(type: "text", nullable: true),
                triggered_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_crawl_job_executions", x => x.id);
                table.ForeignKey(
                    name: "fk_crawl_job_executions_crawl_jobs_crawl_job_id",
                    column: x => x.crawl_job_id,
                    principalTable: "crawl_jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "ix_poi_categories_code", table: "poi_categories", column: "code", unique: true);
        migrationBuilder.CreateIndex(name: "ix_pois_poi_fingerprint", table: "pois", column: "poi_fingerprint", unique: true);
        migrationBuilder.CreateIndex(name: "ix_crawl_jobs_name", table: "crawl_jobs", column: "name", unique: true);
        migrationBuilder.CreateIndex(name: "ix_static_map_snapshots_cache_key", table: "static_map_snapshots", column: "cache_key", unique: true);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS ix_pois_location_gist ON pois USING GIST (location);
            CREATE INDEX IF NOT EXISTS ix_pois_active_location_gist ON pois USING GIST (location) WHERE superseded_at IS NULL;
            CREATE INDEX IF NOT EXISTS ix_h3_cells_centroid_gist ON h3_coverage_cells USING GIST (centroid);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "crawl_job_executions");
        migrationBuilder.DropTable(name: "pois");
        migrationBuilder.DropTable(name: "static_map_snapshots");
        migrationBuilder.DropTable(name: "system_configuration");
        migrationBuilder.DropTable(name: "h3_coverage_cells");
        migrationBuilder.DropTable(name: "crawl_jobs");
        migrationBuilder.DropTable(name: "poi_categories");
    }
}
