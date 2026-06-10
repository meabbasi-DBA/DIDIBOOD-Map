using Didibood.LocationAccess.Application.DataQuality;

namespace Didibood.LocationAccess.Application.Abstractions;

public interface IDataQualityService
{
    Task<DataQualityCompareResult> CompareAsync(DataQualityCompareRequest request, CancellationToken ct = default);

    Task<DataQualityPoiDetailDto?> GetPoiDetailAsync(Guid poiId, CancellationToken ct = default);
}
