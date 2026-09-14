namespace InventoryService.Kafka
{
    public sealed class NoOpBookEventPublisher : IBookEventPublisher
    {
        public Task PublishAsync(BookEvent bookEvent, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}