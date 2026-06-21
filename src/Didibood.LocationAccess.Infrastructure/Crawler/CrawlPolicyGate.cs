using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Didibood.LocationAccess.Infrastructure.Crawler;

public sealed class CrawlPolicyGate(
    AppDbContext db,
    ISystemConfigurationStore configStore,
    ILogger<CrawlPolicyGate> logger) : ICrawlPolicyGate
{
    private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(15);

    public async Task<CrawlGateLease> TryAcquireAsync(
        Guid executionId,
        string source,
        CrawlCell cell,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var normalizedSource = source.Contains("manual", StringComparison.OrdinalIgnoreCase)
            ? "manual"
            : "scheduler";

        var coverageCell = await db.H3CoverageCells
            .FirstOrDefaultAsync(c => c.H3Index == cell.H3Index, ct);

        var gridNumber = coverageCell?.GridNumber;
        var limit = await configStore.GetAsync("crawl.maxExecutionsPerHour", 5, ct);
        var used = await CountAcceptedCallsThisHourAsync(now, ct);

        if (used >= limit)
        {
            var lease = Rejected("rate_limit_exceeded", gridNumber, now);
            await WriteAttemptAsync(lease, accepted: false, "rate_limit_exceeded", ct);
            logger.LogWarning(
                "Crawl execution rejected by rate limit: source={Source}, grid={GridNumber}, h3={H3Index}, used={Used}, limit={Limit}",
                normalizedSource, gridNumber, cell.H3Index, used, limit);
            return lease;
        }

        if (coverageCell is null)
        {
            var lease = Rejected("grid_not_found", null, now);
            await WriteAttemptAsync(lease, accepted: false, "grid_not_found", ct);
            return lease;
        }

        if (coverageCell.CrawlLockExpiresAt is not null && coverageCell.CrawlLockExpiresAt > now)
        {
            var lease = Rejected("grid_locked", gridNumber, now);
            await WriteAttemptAsync(lease, accepted: false, "grid_locked", ct);
            return lease;
        }

        if (coverageCell.NextEligibleCrawlAt is not null && coverageCell.NextEligibleCrawlAt > now)
        {
            var lease = Rejected("not_eligible_yet", gridNumber, now);
            await WriteAttemptAsync(lease, accepted: false, "not_eligible_yet", ct);
            return lease;
        }

        coverageCell.CrawlLockOwner = executionId.ToString("N");
        coverageCell.CrawlLockExpiresAt = now.Add(LockTtl);
        coverageCell.CrawlAttemptCount++;
        coverageCell.LastCrawlAt = now;
        coverageCell.LastCrawlStatus = "running";
        coverageCell.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        var acceptedLease = new CrawlGateLease(
            true,
            "accepted",
            executionId,
            normalizedSource,
            cell.H3Index,
            gridNumber,
            cell.CategoryId,
            cell.SearchTerm,
            now);

        await WriteAttemptAsync(acceptedLease, accepted: true, "accepted", ct);
        return acceptedLease;

        CrawlGateLease Rejected(string reason, int? rejectedGridNumber, DateTimeOffset timestamp) =>
            new(false, reason, executionId, normalizedSource, cell.H3Index, rejectedGridNumber, cell.CategoryId, cell.SearchTerm, timestamp);
    }

    public async Task RecordResultAsync(
        CrawlGateLease lease,
        CrawlExecutionResult result,
        long durationMs,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var succeeded = result.Error is null;

        var cell = await db.H3CoverageCells.FirstOrDefaultAsync(c => c.H3Index == lease.H3Index, ct);
        if (cell is not null)
        {
            if (succeeded)
                cell.CrawlSuccessCount++;

            cell.LastCrawlStatus = succeeded ? "success" : "failed";
            cell.LastCrawlDurationMs = durationMs;
            cell.LastError = result.Error;
            cell.NextEligibleCrawlAt = succeeded ? now.AddDays(30) : now.AddHours(1);
            cell.CoverageScore = Math.Min(1, Math.Max(0, cell.PoiCount / 20.0));
            cell.CrawlLockOwner = null;
            cell.CrawlLockExpiresAt = null;
            cell.UpdatedAt = now;
        }

        db.CrawlHistory.Add(new CrawlHistory
        {
            Id = Guid.NewGuid(),
            ExecutionId = lease.ExecutionId,
            Source = lease.Source,
            GridNumber = lease.GridNumber,
            H3Index = lease.H3Index,
            CategoryId = lease.CategoryId,
            SearchTerm = lease.SearchTerm,
            Timestamp = now,
            DurationMs = durationMs,
            Status = succeeded ? "success" : "failed",
            Reason = succeeded ? "completed" : "execution_failed",
            ResultCount = result.NewRecords + result.UpdatedRecords,
            ErrorMessage = result.Error
        });

        db.NeshanUsageLedger.Add(new NeshanUsageLedger
        {
            Id = Guid.NewGuid(),
            ExecutionId = lease.ExecutionId,
            Source = lease.Source,
            Endpoint = "search",
            GridNumber = lease.GridNumber,
            H3Index = lease.H3Index,
            CategoryId = lease.CategoryId,
            SearchTerm = lease.SearchTerm,
            Timestamp = now,
            Accepted = true,
            Reason = succeeded ? "completed" : "execution_failed",
            CostUnits = Math.Max(1, result.RequestCount),
            DurationMs = durationMs,
            ErrorMessage = result.Error
        });

        await db.SaveChangesAsync(ct);
    }

    private async Task<int> CountAcceptedCallsThisHourAsync(DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now.AddHours(-1);
        return await db.NeshanUsageLedger
            .AsNoTracking()
            .Where(x => x.Accepted && x.Timestamp >= cutoff)
            .SumAsync(x => (int?)x.CostUnits, ct) ?? 0;
    }

    private async Task WriteAttemptAsync(CrawlGateLease lease, bool accepted, string reason, CancellationToken ct)
    {
        db.CrawlHistory.Add(new CrawlHistory
        {
            Id = Guid.NewGuid(),
            ExecutionId = lease.ExecutionId,
            Source = lease.Source,
            GridNumber = lease.GridNumber,
            H3Index = lease.H3Index,
            CategoryId = lease.CategoryId,
            SearchTerm = lease.SearchTerm,
            Timestamp = lease.Timestamp,
            Status = accepted ? "accepted" : "rejected",
            Reason = reason
        });

        db.NeshanUsageLedger.Add(new NeshanUsageLedger
        {
            Id = Guid.NewGuid(),
            ExecutionId = lease.ExecutionId,
            Source = lease.Source,
            Endpoint = "search",
            GridNumber = lease.GridNumber,
            H3Index = lease.H3Index,
            CategoryId = lease.CategoryId,
            SearchTerm = lease.SearchTerm,
            Timestamp = lease.Timestamp,
            Accepted = accepted,
            Reason = reason,
            CostUnits = accepted ? 1 : 0
        });

        await db.SaveChangesAsync(ct);
    }
}
