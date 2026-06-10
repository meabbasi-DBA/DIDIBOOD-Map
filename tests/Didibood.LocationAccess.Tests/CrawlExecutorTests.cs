using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Application.Neshan;
using Didibood.LocationAccess.Domain.Exceptions;
using Didibood.LocationAccess.Infrastructure.Crawler;
using Microsoft.Extensions.Logging.Abstractions;

namespace Didibood.LocationAccess.Tests;

public class CrawlExecutorTests
{
    // ── Fakes ──────────────────────────────────────────────────────────────

    private sealed class FakeSearchClient : INeshanSearchClient
    {
        private readonly Queue<Func<Task<NeshanSearchResponse>>> _responses = new();

        public void Enqueue(NeshanSearchResponse response)
            => _responses.Enqueue(() => Task.FromResult(response));

        public void EnqueueException(Exception ex)
            => _responses.Enqueue(() => Task.FromException<NeshanSearchResponse>(ex));

        public Task<NeshanSearchResponse> SearchAsync(
            string term, double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            if (_responses.TryDequeue(out var factory))
                return factory();

            return Task.FromResult(new NeshanSearchResponse { Items = [] });
        }
    }

    private sealed class FakeNormalizer : IPoiNormalizer
    {
        public bool ReturnNull { get; set; }

        public NormalizedPoi? Normalize(NeshanSearchItem item, short categoryId)
        {
            if (ReturnNull) return null;

            return new NormalizedPoi
            {
                Fingerprint = $"fp_{item.Title}_{categoryId}",
                Title = item.Title,
                CategoryId = categoryId,
                NeshanType = item.Type,
                NeshanCategory = item.Category,
                SourcePayloadJson = "{}",
                Latitude = item.Location.Y,
                Longitude = item.Location.X,
            };
        }
    }

    private sealed class FakeRepository : IPoiRepository
    {
        private readonly HashSet<string> _existing;
        public List<string> UpsertedFingerprints { get; } = [];

        public FakeRepository(IEnumerable<string>? existingFingerprints = null)
            => _existing = existingFingerprints is null
                ? []
                : [..existingFingerprints];

        public Task<bool> ExistsByFingerprintAsync(string fingerprint, CancellationToken ct = default)
            => Task.FromResult(_existing.Contains(fingerprint));

        public Task UpsertAsync(NormalizedPoi poi, CancellationToken ct = default)
        {
            UpsertedFingerprints.Add(poi.Fingerprint);
            return Task.CompletedTask;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static CrawlCell MakeCell(short categoryId = 6)
        => new(H3Index: 1L, Lat: 35.69, Lng: 51.39, CategoryId: categoryId, SearchTerm: "بیمارستان");

    private static NeshanSearchResponse MakeResponse(params string[] titles)
        => new()
        {
            Items = titles.Select(t => new NeshanSearchItem
            {
                Title = t,
                Category = "place",
                Type = "hospital",
                Location = new NeshanLocation { X = 51.39, Y = 35.69 },
            }).ToList(),
        };

    private static CrawlExecutor MakeExecutor(
        FakeSearchClient client,
        FakeNormalizer? normalizer = null,
        FakeRepository? repository = null)
        => new(
            client,
            normalizer ?? new FakeNormalizer(),
            repository ?? new FakeRepository(),
            NullLogger<CrawlExecutor>.Instance);

    // ── New records ────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NewItems_CountsNewRecords()
    {
        var client = new FakeSearchClient();
        client.Enqueue(MakeResponse("بیمارستان الف", "بیمارستان ب"));

        var result = await MakeExecutor(client).ExecuteAsync(MakeCell());

        Assert.Equal(2, result.NewRecords);
        Assert.Equal(0, result.UpdatedRecords);
        Assert.Equal(0, result.FailedRecords);
        Assert.Equal(1, result.RequestCount);
        Assert.Null(result.Error);
    }

    // ── Updated records ────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ExistingItems_CountsUpdatedRecords()
    {
        var client = new FakeSearchClient();
        client.Enqueue(MakeResponse("بیمارستان الف", "بیمارستان ب"));

        // Pre-seed fingerprints that the normalizer will produce.
        var repo = new FakeRepository(existingFingerprints:
        [
            "fp_بیمارستان الف_6",
            "fp_بیمارستان ب_6",
        ]);

        var result = await MakeExecutor(client, repository: repo).ExecuteAsync(MakeCell());

        Assert.Equal(0, result.NewRecords);
        Assert.Equal(2, result.UpdatedRecords);
        Assert.Equal(0, result.FailedRecords);
    }

    // ── Mixed new + updated ────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_MixedItems_CountsBoth()
    {
        var client = new FakeSearchClient();
        client.Enqueue(MakeResponse("بیمارستان جدید", "بیمارستان قدیمی"));

        var repo = new FakeRepository(existingFingerprints: ["fp_بیمارستان قدیمی_6"]);

        var result = await MakeExecutor(client, repository: repo).ExecuteAsync(MakeCell());

        Assert.Equal(1, result.NewRecords);
        Assert.Equal(1, result.UpdatedRecords);
    }

    // ── Normalizer returns null ────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NormalizerReturnsNull_SkipsItem()
    {
        var client = new FakeSearchClient();
        client.Enqueue(MakeResponse("POI A", "POI B"));

        var normalizer = new FakeNormalizer { ReturnNull = true };

        var result = await MakeExecutor(client, normalizer).ExecuteAsync(MakeCell());

        Assert.Equal(0, result.NewRecords);
        Assert.Equal(0, result.UpdatedRecords);
        Assert.Equal(0, result.FailedRecords);
    }

    // ── Empty response ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_EmptyResponse_ReturnsZeroCounts()
    {
        var client = new FakeSearchClient();
        client.Enqueue(new NeshanSearchResponse { Items = [] });

        var result = await MakeExecutor(client).ExecuteAsync(MakeCell());

        Assert.Equal(0, result.NewRecords);
        Assert.Equal(0, result.UpdatedRecords);
        Assert.Equal(0, result.FailedRecords);
        Assert.Null(result.Error);
    }

    // ── Rate limit retry ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_RateLimitThenSuccess_Retries()
    {
        var client = new FakeSearchClient();
        client.EnqueueException(new NeshanRateLimitException("rate limited"));
        client.Enqueue(MakeResponse("بیمارستان پس از ریت لیمیت"));

        var executor = new CrawlExecutor(
            client,
            new FakeNormalizer(),
            new FakeRepository(),
            NullLogger<CrawlExecutor>.Instance);

        // Patch delay to avoid waiting 5s in tests by using a very short CT timeout would
        // fail, so we just run with the real delay suppressed by not injecting Task.Delay.
        // This test verifies retry logic succeeds without throwing.
        var result = await executor.ExecuteAsync(MakeCell(), CancellationToken.None);

        Assert.Equal(1, result.NewRecords);
        Assert.Null(result.Error);
    }

    // ── Quota exceeded ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_QuotaExceeded_ReturnsErrorResult()
    {
        var client = new FakeSearchClient();
        client.EnqueueException(new NeshanQuotaExceededException("quota exceeded"));

        var result = await MakeExecutor(client).ExecuteAsync(MakeCell());

        Assert.NotNull(result.Error);
        Assert.Equal(0, result.NewRecords);
        Assert.Equal(0, result.UpdatedRecords);
    }

    // ── RequestCount ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SuccessfulCall_HasRequestCountOne()
    {
        var client = new FakeSearchClient();
        client.Enqueue(new NeshanSearchResponse { Items = [] });

        var result = await MakeExecutor(client).ExecuteAsync(MakeCell());

        Assert.Equal(1, result.RequestCount);
    }

    [Fact]
    public async Task ExecuteAsync_OneRateLimitRetry_HasRequestCountTwo()
    {
        var client = new FakeSearchClient();
        client.EnqueueException(new NeshanRateLimitException("rate limited"));
        client.Enqueue(new NeshanSearchResponse { Items = [] });

        var result = await MakeExecutor(client).ExecuteAsync(MakeCell());

        Assert.Equal(2, result.RequestCount);
    }
}
