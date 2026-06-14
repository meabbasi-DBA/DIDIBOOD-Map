using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Didibood.LocationAccess.Infrastructure.Persistence.Migrations;

/// <summary>
/// Ensures boundary refinement config exists and triggers one-time grid reseed.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260616140000_BoundaryRefinementPersistence")]
public partial class BoundaryRefinementPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO system_configuration (config_key, config_value, value_type, description, updated_at, updated_by)
            SELECT 'grid.boundary.refinement.enabled', 'true', 'bool',
                   'Enable virtual sub-centroids inside municipality boundary H3 cells',
                   NOW(), 'migration'
            WHERE NOT EXISTS (
                SELECT 1 FROM system_configuration WHERE config_key = 'grid.boundary.refinement.enabled'
            );

            UPDATE system_configuration
            SET config_value = 'true', updated_at = NOW(), updated_by = 'migration'
            WHERE config_key = 'crawl.h3_reseed_on_startup';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system_configuration WHERE config_key = 'grid.boundary.refinement.enabled';
            """);
    }
}
