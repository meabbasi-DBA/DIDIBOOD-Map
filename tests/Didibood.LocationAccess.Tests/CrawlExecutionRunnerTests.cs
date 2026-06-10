using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Infrastructure.Crawler;

namespace Didibood.LocationAccess.Tests;

public class CrawlExecutionRunnerTests
{
    private static CrawlJob SampleJob() => new()
    {
        Id = Guid.NewGuid(),
        Name = "tehran-daily",
        H3Resolution = 8,
        MaxParallelCells = 2
    };

    [Fact]
    public void ParseManualPlan_FullMode_UsesAllCells()
    {
        var plan = CrawlExecutionRunner.ParseManualPlan("admin_manual:full", SampleJob());
        Assert.False(plan.StaleOnly);
        Assert.Null(plan.CategoryIds);
    }

    [Fact]
    public void ParseManualPlan_CategoryMode_ParsesIds()
    {
        var plan = CrawlExecutionRunner.ParseManualPlan("admin_manual:categories:6,8", SampleJob());
        Assert.Equal([6, 8], plan.CategoryIds);
        Assert.False(plan.StaleOnly);
    }

    [Fact]
    public void ParseManualPlan_FailedMode_UsesStaleOnly()
    {
        var plan = CrawlExecutionRunner.ParseManualPlan("admin_manual:failed", SampleJob());
        Assert.True(plan.StaleOnly);
    }
}
