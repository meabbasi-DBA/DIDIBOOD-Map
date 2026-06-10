using Didibood.LocationAccess.Application.Coverage;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Infrastructure.Coverage;
using Didibood.LocationAccess.Infrastructure.H3;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Didibood.LocationAccess.Tests;

public class CoverageServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_ComputesCoveragePercent()
    {
        await using var db = CreateDb();
        var cells = H3CellGeometry.PolyfillBounds(35.68, 35.70, 51.38, 51.40, 8).Take(4).ToList();
        var now = DateTimeOffset.UtcNow;

        foreach (var (h3, status) in cells.Zip(new[] { "success", "success", "pending", "failed" }))
            AddCell(db, h3, status, now);

        await db.SaveChangesAsync();

        var service = new CoverageService(db);
        var summary = await service.GetSummaryAsync();

        Assert.Equal(4, summary.TotalCells);
        Assert.Equal(2, summary.SuccessCells);
        Assert.Equal(50.0, summary.CoveragePercent);
    }

    [Fact]
    public async Task GetCellsAsync_ReturnsGeoJsonFeatures()
    {
        await using var db = CreateDb();
        var h3 = H3CellGeometry.PolyfillBounds(35.68, 35.70, 51.38, 51.40, 8)[0];
        AddCell(db, h3, "pending", DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var service = new CoverageService(db);
        var geoJson = await service.GetCellsAsync(new CoverageCellsQuery { Limit = 10 });

        Assert.Single(geoJson.Features);
        Assert.Equal(h3, geoJson.Features[0].Properties.H3Index);
        Assert.NotEmpty(geoJson.Features[0].Geometry.Coordinates);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static void AddCell(AppDbContext db, long h3Index, string status, DateTimeOffset now)
    {
        var (lat, lng) = H3CellGeometry.GetCentroid(h3Index);
        var cell = new H3CoverageCell
        {
            H3Index = h3Index,
            Resolution = 8,
            Status = status,
            PoiCount = 3,
            CreatedAt = now,
            UpdatedAt = now,
            LastSuccessAt = status == "success" ? now : null
        };

        db.H3CoverageCells.Add(cell);
        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        db.Entry(cell).Property("Centroid").CurrentValue = geometryFactory.CreatePoint(new Coordinate(lng, lat));
    }
}
