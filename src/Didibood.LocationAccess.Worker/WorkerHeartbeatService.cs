namespace Didibood.LocationAccess.Worker;

public sealed class WorkerHeartbeatService(ILogger<WorkerHeartbeatService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Didibood Location Access Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("Worker heartbeat at {Time}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
