using System.Text.Json;
using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Didibood.LocationAccess.Infrastructure.Crawler;

public sealed class CrawlPlanner(AppDbContext db) : ICrawlPlanner
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<CrawlCell>> PlanAsync(CrawlPlanRequest request, CancellationToken ct = default)
    {
        // Query h3_coverage_cells filtered by resolution and optional stale/failed status.
        var cellQuery = db.H3CoverageCells
            .Where(c => c.Resolution == request.Resolution);

        if (request.StaleOnly)
        {
            cellQuery = cellQuery.Where(c => c.Status == "stale" || c.Status == "failed");
        }

        // Load cells with tracking so shadow property Centroid is accessible.
        var cells = await cellQuery.ToListAsync(ct);

        // Query enabled categories, optionally filtered by ID list.
        var categoryQuery = db.PoiCategories
            .Where(c => c.IsEnabled);

        if (request.CategoryIds is { Length: > 0 })
        {
            var ids = request.CategoryIds;
            categoryQuery = categoryQuery.Where(c => ids.Contains(c.Id));
        }

        var categories = await categoryQuery.ToListAsync(ct);

        var result = new List<CrawlCell>(cells.Count * categories.Count);

        foreach (var cell in cells)
        {
            // Extract centroid from shadow PostGIS Point property.
            var centroid = (Point?)db.Entry(cell).Property("Centroid").CurrentValue;
            var lat = centroid?.Y ?? 0.0;
            var lng = centroid?.X ?? 0.0;

            foreach (var category in categories)
            {
                var terms = JsonSerializer.Deserialize<string[]>(category.SearchTermsJson, JsonOpts)
                            ?? [];

                foreach (var term in terms)
                {
                    result.Add(new CrawlCell(
                        H3Index: cell.H3Index,
                        Lat: lat,
                        Lng: lng,
                        CategoryId: category.Id,
                        SearchTerm: term));
                }
            }
        }

        return result;
    }
}
