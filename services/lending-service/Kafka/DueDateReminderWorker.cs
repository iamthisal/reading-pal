namespace LendingService.Kafka;

/// <summary>
/// Runs the due-date reminder check at startup and then every <c>DueDateReminders:IntervalMinutes</c>
/// (default 60). A failed run is logged and the next run catches up, because the check covers every
/// loan due within the reminder window that has not been reminded yet.
/// </summary>
public sealed class DueDateReminderWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration,
    ILogger<DueDateReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = configuration.GetValue("DueDateReminders:IntervalMinutes", 60);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, minutes)));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var queued = await scope.ServiceProvider.GetRequiredService<DueDateReminderService>()
                    .QueueRemindersAsync(DateTime.UtcNow, stoppingToken);
                if (queued > 0) logger.LogInformation("Queued {Count} due-date reminder(s).", queued);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Due-date reminder check failed. Missed reminders will be queued on the next run.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
