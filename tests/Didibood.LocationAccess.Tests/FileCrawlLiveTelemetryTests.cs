using Didibood.LocationAccess.Infrastructure.Crawler;

namespace Didibood.LocationAccess.Tests;

public class FileCrawlLiveTelemetryTests
{
    [Fact]
    public void Snapshot_TracksQueuedCurrentRecentAndFailedCells()
    {
        var telemetry = new FileCrawlLiveTelemetry();
        var executionId = Guid.NewGuid();

        try
        {
            telemetry.SetQueuedCells(executionId, [101, 102, 103]);
            telemetry.SetCurrentCell(executionId, 102, categoryId: 6, searchTerm: "hospital");
            telemetry.SetLiveError(executionId, "temporary warning");
            telemetry.RecordCellResult(executionId, 102, succeeded: false, "provider error");
            telemetry.RecordCellResult(executionId, 101, succeeded: true, null);

            var snapshot = telemetry.GetSnapshot(executionId);

            Assert.Equal("temporary warning", snapshot.Error);
            Assert.DoesNotContain(102, snapshot.QueuedCells);
            Assert.Contains(101, snapshot.QueuedCells);
            Assert.Contains(103, snapshot.QueuedCells);
            Assert.Null(snapshot.CurrentCell);
            Assert.Equal([101, 102], snapshot.RecentCells.Select(c => c.H3Index).ToArray());
            var failed = Assert.Single(snapshot.FailedCells);
            Assert.Equal(102, failed.H3Index);
            Assert.Equal("provider error", failed.Error);
        }
        finally
        {
            telemetry.Clear(executionId);
        }
    }
}
