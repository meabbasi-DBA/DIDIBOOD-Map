# Quick grid probe — run via: dotnet script or inline C# in test
$code = @'
using Didibood.LocationAccess.Infrastructure.H3;

const int Terms = 27;
var bounds = new[] {
    ("loose", 35.48, 35.92, 51.08, 51.65),
    ("tight", 35.57, 35.82, 51.15, 51.55),
    ("muni", 35.56, 35.84, 51.12, 51.58)
};

foreach (var (name, minLat, maxLat, minLng, maxLng) in bounds)
{
    Console.WriteLine($"=== {name} ===");
    foreach (var res in new[] { 6, 7, 8 })
    {
        var cells = H3CellGeometry.PolyfillBounds(minLat, maxLat, minLng, maxLng, res);
        var filtered = cells.Where(h =>
        {
            var (lat, lng) = H3CellGeometry.GetCentroid(h);
            return lat >= minLat && lat <= maxLat && lng >= minLng && lng <= maxLng;
        }).ToList();

        for (var radius = 1500; radius <= 4000; radius += 250)
        {
            if (H3GridPlanner.ValidateOverlap(filtered, radius, minLat, maxLat, minLng, maxLng, 200))
            {
                var req = filtered.Count * Terms;
                Console.WriteLine($"  res{res}: cells={filtered.Count} radius>={radius}m requests={req}");
                break;
            }
        }
    }
}
'@
