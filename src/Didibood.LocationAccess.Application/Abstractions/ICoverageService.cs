using Didibood.LocationAccess.Application.Coverage;

namespace Didibood.LocationAccess.Application.Abstractions;

public interface ICoverageService
{
    Task<CoverageSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    Task<CoverageGeoJsonDto> GetCellsAsync(CoverageCellsQuery query, CancellationToken ct = default);

    Task<CoverageCellDetailDto?> GetCellDetailAsync(long h3Index, CancellationToken ct = default);

    Task<IReadOnlyList<HeatmapPointDto>> GetHeatmapAsync(CoverageHeatmapQuery query, CancellationToken ct = default);

    Task<CoverageBoundaryGeoJsonDto> GetBoundaryAsync(CancellationToken ct = default);

    Task<CoverageDebugDto> GetDebugAsync(CancellationToken ct = default);

    Task<CoverageRefinementDebugDto> GetRefinementDebugAsync(CancellationToken ct = default);
}
