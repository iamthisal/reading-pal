namespace LendingService.Kafka;

public sealed class ReservationOutboxWorker(IServiceScopeFactory scopeFactory, ILogger<ReservationOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ReservationOutboxDispatcher>().DispatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Reservation event delivery failed. Unpublished events will be retried.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
