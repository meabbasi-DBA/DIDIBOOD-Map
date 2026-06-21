using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Didibood.LocationAccess.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260621120000_CrawlGovernanceAndMonitoring")]
public partial class CrawlGovernanceAndMonitoring : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE h3_coverage_cells
                ADD COLUMN IF NOT EXISTS grid_number integer NULL,
                ADD COLUMN IF NOT EXISTS crawl_attempt_count integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS crawl_success_count integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS last_crawl_status character varying(20) NULL,
                ADD COLUMN IF NOT EXISTS last_crawl_duration_ms bigint NULL,
                ADD COLUMN IF NOT EXISTS last_error text NULL,
                ADD COLUMN IF NOT EXISTS next_eligible_crawl_at timestamp with time zone NULL,
                ADD COLUMN IF NOT EXISTS coverage_score double precision NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS crawl_lock_owner character varying(100) NULL,
                ADD COLUMN IF NOT EXISTS crawl_lock_expires_at timestamp with time zone NULL;

            WITH ordered AS (
                SELECT h3_index, ROW_NUMBER() OVER (ORDER BY ST_Y(centroid::geometry) DESC, ST_X(centroid::geometry), h3_index) AS rn
                FROM h3_coverage_cells
                WHERE grid_number IS NULL
            )
            UPDATE h3_coverage_cells c
            SET grid_number = ordered.rn
            FROM ordered
            WHERE c.h3_index = ordered.h3_index;

            CREATE UNIQUE INDEX IF NOT EXISTS ix_h3_coverage_cells_grid_number
                ON h3_coverage_cells(grid_number)
                WHERE grid_number IS NOT NULL;

            CREATE INDEX IF NOT EXISTS ix_h3_coverage_cells_status_next_eligible
                ON h3_coverage_cells(status, next_eligible_crawl_at);

            CREATE INDEX IF NOT EXISTS ix_h3_coverage_cells_crawl_lock
                ON h3_coverage_cells(crawl_lock_owner, crawl_lock_expires_at);

            CREATE TABLE IF NOT EXISTS crawl_history (
                id uuid PRIMARY KEY,
                execution_id uuid NULL,
                source character varying(50) NOT NULL,
                grid_number integer NULL,
                h3_index bigint NOT NULL,
                category_id smallint NULL,
                search_term character varying(200) NULL,
                timestamp timestamp with time zone NOT NULL,
                duration_ms bigint NULL,
                status character varying(20) NOT NULL,
                reason text NULL,
                result_count integer NOT NULL DEFAULT 0,
                error_message text NULL
            );

            CREATE INDEX IF NOT EXISTS ix_crawl_history_timestamp ON crawl_history(timestamp);
            CREATE INDEX IF NOT EXISTS ix_crawl_history_grid_number ON crawl_history(grid_number);
            CREATE INDEX IF NOT EXISTS ix_crawl_history_status ON crawl_history(status);

            CREATE TABLE IF NOT EXISTS neshan_usage_ledger (
                id uuid PRIMARY KEY,
                execution_id uuid NULL,
                source character varying(50) NOT NULL,
                endpoint character varying(50) NOT NULL,
                grid_number integer NULL,
                h3_index bigint NULL,
                category_id smallint NULL,
                search_term character varying(200) NULL,
                timestamp timestamp with time zone NOT NULL,
                accepted boolean NOT NULL,
                reason character varying(200) NOT NULL,
                cost_units integer NOT NULL DEFAULT 1,
                duration_ms bigint NULL,
                http_status integer NULL,
                error_message text NULL
            );

            CREATE INDEX IF NOT EXISTS ix_neshan_usage_ledger_timestamp ON neshan_usage_ledger(timestamp);
            CREATE INDEX IF NOT EXISTS ix_neshan_usage_ledger_accepted_timestamp ON neshan_usage_ledger(accepted, timestamp);
            CREATE INDEX IF NOT EXISTS ix_neshan_usage_ledger_grid_number ON neshan_usage_ledger(grid_number);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS neshan_usage_ledger;
            DROP TABLE IF EXISTS crawl_history;
            DROP INDEX IF EXISTS ix_h3_coverage_cells_crawl_lock;
            DROP INDEX IF EXISTS ix_h3_coverage_cells_status_next_eligible;
            DROP INDEX IF EXISTS ix_h3_coverage_cells_grid_number;
            ALTER TABLE h3_coverage_cells
                DROP COLUMN IF EXISTS crawl_lock_expires_at,
                DROP COLUMN IF EXISTS crawl_lock_owner,
                DROP COLUMN IF EXISTS coverage_score,
                DROP COLUMN IF EXISTS next_eligible_crawl_at,
                DROP COLUMN IF EXISTS last_error,
                DROP COLUMN IF EXISTS last_crawl_duration_ms,
                DROP COLUMN IF EXISTS last_crawl_status,
                DROP COLUMN IF EXISTS crawl_success_count,
                DROP COLUMN IF EXISTS crawl_attempt_count,
                DROP COLUMN IF EXISTS grid_number;
            """);
    }
}
