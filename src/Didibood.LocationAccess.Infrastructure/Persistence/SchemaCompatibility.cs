using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Infrastructure.Persistence;

/// <summary>
/// Idempotent schema patches for databases that predate EF migration discovery fixes.
/// </summary>
internal static class SchemaCompatibility
{
    public static async Task EnsureH3CoverageColumnsAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE h3_coverage_cells
                ADD COLUMN IF NOT EXISTS parent_h3_index bigint NULL,
                ADD COLUMN IF NOT EXISTS is_refined boolean NOT NULL DEFAULT false,
                ADD COLUMN IF NOT EXISTS request_count integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS municipality_mode boolean NOT NULL DEFAULT false;
            """,
            ct);
    }
}
