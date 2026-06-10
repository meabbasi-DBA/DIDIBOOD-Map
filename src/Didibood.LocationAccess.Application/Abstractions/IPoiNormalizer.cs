using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Application.Neshan;

namespace Didibood.LocationAccess.Application.Abstractions;

public interface IPoiNormalizer
{
    /// <summary>
    /// Normalizes a raw Neshan search result into a canonical POI.
    /// Returns null if the item should be filtered out (wrong neshan_category or type not in whitelist).
    /// </summary>
    NormalizedPoi? Normalize(NeshanSearchItem item, short categoryId);
}
