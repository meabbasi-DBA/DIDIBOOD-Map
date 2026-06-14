using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Didibood.LocationAccess.Infrastructure.Persistence.Migrations;

/// <summary>
/// Tightens Tehran bounds, sets optimized search radius, and clears the legacy res-8 grid
/// so startup reseeds ~373 cells at resolution 7 (~10k Neshan requests/crawl).
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260614120000_TightenTehranH3Grid")]
public partial class TightenTehranH3Grid : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE system_configuration
            SET config_value = '{"minLat":35.50,"maxLat":35.88,"minLng":51.10,"maxLng":51.62}',
                updated_at = NOW(),
                updated_by = 'migration'
            WHERE config_key = 'tehran.bounds';

            UPDATE system_configuration
            SET config_value = '2100',
                updated_at = NOW(),
                updated_by = 'migration'
            WHERE config_key = 'search.radius.default_meters';

            DELETE FROM h3_coverage_cells;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE system_configuration
            SET config_value = '{"minLat":35.48,"maxLat":35.92,"minLng":51.08,"maxLng":51.65}',
                updated_at = NOW(),
                updated_by = 'migration'
            WHERE config_key = 'tehran.bounds';

            UPDATE system_configuration
            SET config_value = '2000',
                updated_at = NOW(),
                updated_by = 'migration'
            WHERE config_key = 'search.radius.default_meters';
            """);
    }
}
