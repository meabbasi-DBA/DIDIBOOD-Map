using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Application.Neshan;
using Didibood.LocationAccess.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Didibood.LocationAccess.Infrastructure.Crawler;

public sealed class CrawlExecutor(
    INeshanSearchClient searchClient,
    IPoiNormalizer normalizer,
    IPoiRepository repository,
    ILogger<CrawlExecutor> logger) : ICrawlExecutor
{
    private const int MaxRateLimitRetries = 3;
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromSeconds(5);

    public async Task<CrawlExecutionResult> ExecuteAsync(CrawlCell cell, CancellationToken ct = default)
    {
        var (response, requestCount) = await CallWithRetryAsync(cell, ct);
        if (response is null)
        {
            // Quota exceeded — caller should stop dispatching further cells.
            return new CrawlExecutionResult(
                NewRecords: 0,
                UpdatedRecords: 0,
                FailedRecords: 0,
                RequestCount: requestCount,
                Error: "Neshan daily quota exceeded.");
        }

        int newRecords = 0;
        int updatedRecords = 0;
        int failedRecords = 0;
        var skipped = 0;

        foreach (var item in response.Items)
        {
            try
            {
                var poi = normalizer.Normalize(item, cell.CategoryId);
                if (poi is null)
                {
                    skipped++;
                    continue;
                }

                var exists = await repository.ExistsByFingerprintAsync(poi.Fingerprint, ct);
                await repository.UpsertAsync(poi, ct);

                if (exists)
                    updatedRecords++;
                else
                    newRecords++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failedRecords++;
                logger.LogWarning(ex,
                    "Failed to persist POI '{Title}' for cell H3={H3Index}.",
                    item.Title, cell.H3Index);
            }
        }

        if (response.Items.Count > 0 && newRecords + updatedRecords == 0)
        {
            logger.LogDebug(
                "Cell H3={H3Index} term={Term}: API returned {ItemCount} items, normalized 0 (skipped {Skipped})",
                cell.H3Index,
                cell.SearchTerm,
                response.Items.Count,
                skipped);
        }

        return new CrawlExecutionResult(
            NewRecords: newRecords,
            UpdatedRecords: updatedRecords,
            FailedRecords: failedRecords,
            RequestCount: requestCount);
    }

    private async Task<(NeshanSearchResponse? Response, int RequestCount)> CallWithRetryAsync(
        CrawlCell cell, CancellationToken ct)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                var result = await searchClient.SearchAsync(cell.SearchTerm, cell.Lat, cell.Lng, ct);
                return (result, attempt + 1);
            }
            catch (NeshanRateLimitException) when (attempt < MaxRateLimitRetries)
            {
                attempt++;
                logger.LogWarning(
                    "Neshan rate limit hit for cell H3={H3Index} (attempt {Attempt}/{Max}). Waiting {Delay}s.",
                    cell.H3Index, attempt, MaxRateLimitRetries, RateLimitDelay.TotalSeconds);

                await Task.Delay(RateLimitDelay, ct);
            }
            catch (NeshanQuotaExceededException ex)
            {
                logger.LogCritical(ex,
                    "Neshan daily quota exceeded for cell H3={H3Index}. Crawl must stop.",
                    cell.H3Index);
                return (null, attempt + 1);
            }
        }
    }
}
