using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Didibood.LocationAccess.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds municipality_mode flag, persists boundary config, clears legacy bbox grid.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260615130000_MunicipalityBoundaryGrid")]
public partial class MunicipalityBoundaryGrid : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE h3_coverage_cells
                ADD COLUMN IF NOT EXISTS parent_h3_index bigint NULL,
                ADD COLUMN IF NOT EXISTS is_refined boolean NOT NULL DEFAULT false,
                ADD COLUMN IF NOT EXISTS request_count integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS municipality_mode boolean NOT NULL DEFAULT false;

            INSERT INTO system_configuration (config_key, config_value, value_type, description, updated_at, updated_by)
            SELECT 'tehran.boundary.mode', 'municipality', 'string',
                   'Tehran grid source: municipality (22-district polygon) or bbox (legacy rectangle)',
                   NOW(), 'migration'
            WHERE NOT EXISTS (
                SELECT 1 FROM system_configuration WHERE config_key = 'tehran.boundary.mode'
            );

            UPDATE system_configuration
            SET config_value = '2000', updated_at = NOW(), updated_by = 'migration'
            WHERE config_key = 'search.radius.default_meters';

            UPDATE system_configuration
            SET config_value = 'true', updated_at = NOW(), updated_by = 'migration'
            WHERE config_key = 'crawl.h3_reseed_on_startup';

            DELETE FROM h3_coverage_cells;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE h3_coverage_cells
                DROP COLUMN IF EXISTS municipality_mode,
                DROP COLUMN IF EXISTS parent_h3_index,
                DROP COLUMN IF EXISTS is_refined,
                DROP COLUMN IF EXISTS request_count;

            DELETE FROM system_configuration WHERE config_key = 'tehran.boundary.mode';
            """);
    }
}
