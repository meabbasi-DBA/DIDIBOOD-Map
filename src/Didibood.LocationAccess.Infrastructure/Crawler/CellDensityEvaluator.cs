using Didibood.LocationAccess.Application.Crawler;

namespace Didibood.LocationAccess.Infrastructure.Crawler;

/// <summary>
/// Phase 3: decides whether a res-7 cell should subdivide into res-8 children.
/// </summary>
public static class CellDensityEvaluator
{
    public const int ApiNearLimitThreshold = 28;
    public const int PoiDensityThreshold = 120;
    public const int SaturatedCategoryThreshold = 3;

    public static CellDensitySignal Evaluate(
        long h3Index,
        int apiResultCount,
        int poiCountAfterNormalize,
        int categoriesSaturated)
    {
        var isDense = apiResultCount >= ApiNearLimitThreshold
                      || poiCountAfterNormalize >= PoiDensityThreshold
                      || categoriesSaturated >= SaturatedCategoryThreshold;

        return new CellDensitySignal
        {
            H3Index = h3Index,
            ApiResultCount = apiResultCount,
            PoiCountAfterNormalize = poiCountAfterNormalize,
            CategoriesSaturated = categoriesSaturated,
            IsDense = isDense
        };
    }
}
