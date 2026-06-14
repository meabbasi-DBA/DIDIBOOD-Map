using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Didibood.LocationAccess.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260613130000_ExtendCrawlExecutionFields")]
public partial class ExtendCrawlExecutionFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE crawl_job_executions
                ADD COLUMN IF NOT EXISTS total_tasks_planned integer NOT NULL DEFAULT 0;

            ALTER TABLE crawl_job_executions
                ALTER COLUMN triggered_by TYPE character varying(256);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE crawl_job_executions
                DROP COLUMN IF EXISTS total_tasks_planned;

            ALTER TABLE crawl_job_executions
                ALTER COLUMN triggered_by TYPE character varying(50);
            """);
    }
}
