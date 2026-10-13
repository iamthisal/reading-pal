namespace LendingService.Kafka;

/// <summary>
/// Runs <see cref="ReservationSnapshotService"/> once at startup, retrying every minute until it
/// succeeds (for example while the database is still starting).
/// </summary>
public sealed class ReservationSnapshotWorker(IServiceScopeFactory scopeFactory, ILogger<ReservationSnapshotWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var queued = await scope.ServiceProvider.GetRequiredService<ReservationSnapshotService>().QueueSnapshotsAsync(stoppingToken);
                if (queued > 0) logger.LogInformation("Queued {Count} reservation snapshot(s).", queued);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Reservation snapshot backfill failed; retrying in a minute.");
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }
}
