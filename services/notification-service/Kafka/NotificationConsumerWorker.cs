using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NotificationService.Services;

namespace NotificationService.Kafka;

/// <summary>
/// Reads Lending's events and stores a notification for each one. Offsets are committed only after a
/// message is handled, so a crash or database outage causes a redelivery rather than a lost notification;
/// <see cref="LendingEventHandler"/> makes those redeliveries harmless.
/// </summary>
public sealed class NotificationConsumerWorker(
    IOptions<KafkaOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationConsumerWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    // Consume() blocks, so run the loop off the startup thread.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => ConsumeLoopAsync(stoppingToken), stoppingToken);

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.BootstrapServers)
            || !Enum.TryParse<SecurityProtocol>(settings.SecurityProtocol, true, out var protocol))
        {
            logger.LogError("Kafka BootstrapServers or SecurityProtocol is not configured; notifications will not be consumed.");
            return;
        }

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            SecurityProtocol = protocol,
            GroupId = settings.GroupId,
            EnableAutoCommit = false,
            // On the very first start, also process events already waiting in the topics.
            AutoOffsetReset = AutoOffsetReset.Earliest
        })
        .SetErrorHandler((_, error) => logger.LogWarning("Kafka consumer error: {Reason}", error.Reason))
        .Build();

        consumer.Subscribe(new[] { settings.Topics.ReservationCreated, settings.Topics.ReservationAccepted, settings.Topics.ReservationCancelled, settings.Topics.BookReturned, settings.Topics.FineRecorded });
        logger.LogInformation("Notification consumer subscribed as group {GroupId}.", settings.GroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = consumer.Consume(stoppingToken);
                    if (result?.Message == null) continue;

                    using var scope = scopeFactory.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<LendingEventHandler>();
                    var outcome = await handler.HandleAsync(result.Message.Value, stoppingToken);
                    consumer.Commit(result);
                    logger.LogInformation("Handled {Topic} message at offset {Offset}: {Outcome}.",
                        result.Topic, result.Offset.Value, outcome);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    logger.LogWarning(ex, "Kafka consume failed; retrying.");
                    await Task.Delay(RetryDelay, stoppingToken);
                }
                catch (Exception ex)
                {
                    // Usually the database is unavailable. Rewind so the same message is retried, not skipped.
                    logger.LogError(ex, "Failed to store a notification; the message will be retried.");
                    if (result != null) consumer.Seek(result.TopicPartitionOffset);
                    await Task.Delay(RetryDelay, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            consumer.Close();
        }
    }
}
