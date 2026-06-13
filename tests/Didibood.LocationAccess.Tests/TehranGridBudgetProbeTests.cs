using Didibood.LocationAccess.Infrastructure.H3;

namespace Didibood.LocationAccess.Tests;

public class TehranGridBudgetProbeTests
{
    private const int SearchTermsPerCrawl = 27;

    public static TheoryData<string, double, double, double, double> BoundSets => new()
    {
        { "loose", 35.48, 35.92, 51.08, 51.65 },
        { "mid1", 35.50, 35.88, 51.10, 51.62 },
        { "mid2", 35.51, 35.87, 51.11, 51.61 },
        { "mid3", 35.52, 35.86, 51.11, 51.60 },
        { "tight", 35.57, 35.82, 51.15, 51.55 },
        { "muni", 35.56, 35.84, 51.12, 51.58 },
    };

    [Theory]
    [MemberData(nameof(BoundSets))]
    public void Probe_MinRadius_And_RequestCount(
        string name, double minLat, double maxLat, double minLng, double maxLng)
    {
        foreach (var res in new[] { 6, 7, 8 })
        {
            var cells = H3CellGeometry.PolyfillBounds(minLat, maxLat, minLng, maxLng, res);
            int? minRadius = null;
            for (var radius = 1500; radius <= 4500; radius += 100)
            {
                if (H3GridPlanner.ValidateOverlap(cells, radius, minLat, maxLat, minLng, maxLng, samplePoints: 200))
                {
                    minRadius = radius;
                    break;
                }
            }

            var requests = cells.Count * SearchTermsPerCrawl;
            Console.WriteLine($"{name} res{res}: cells={cells.Count} minRadius={minRadius?.ToString() ?? "FAIL"} requests={requests}");
        }
    }
}
