namespace InventoryService.Kafka
{
    public interface IBookEventPublisher
    {
        Task PublishAsync(BookEvent bookEvent, CancellationToken cancellationToken = default);
    }
}