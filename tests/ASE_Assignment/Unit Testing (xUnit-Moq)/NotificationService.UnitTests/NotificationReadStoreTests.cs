using NotificationService.Services;

namespace NotificationService.UnitTests;

public class NotificationReadStoreTests
{
    [Fact]
    // Proves duplicate notification IDs create only one read record.
    public async Task MarkReadAsync_DuplicateIds_AddsOneReadRecord()
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        var store = new NotificationReadStore(db);
        // Act
        var count = await store.MarkReadAsync("user:5", new[] { 10, 10 }, CancellationToken.None);
        // Assert
        Assert.Equal(1, count);
        Assert.Equal(1, db.NotificationReads.Count());
    }

    [Fact]
    // Proves marking an already-read notification is harmless.
    public async Task MarkReadAsync_AlreadyRead_AddsNothing()
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        var store = new NotificationReadStore(db);
        await store.MarkReadAsync("user:5", new[] { 10 }, CancellationToken.None);
        // Act
        var count = await store.MarkReadAsync("user:5", new[] { 10 }, CancellationToken.None);
        // Assert
        Assert.Equal(0, count);
        Assert.Equal(1, db.NotificationReads.Count());
    }

    [Fact]
    // Proves one recipient reading a notification does not mark it read for another.
    public async Task MarkReadAsync_DifferentRecipients_KeepsSeparateReadRecords()
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        var store = new NotificationReadStore(db);
        await store.MarkReadAsync("user:5", new[] { 10 }, CancellationToken.None);
        // Act
        var count = await store.MarkReadAsync("user:6", new[] { 10 }, CancellationToken.None);
        // Assert
        Assert.Equal(1, count);
        Assert.Equal(2, db.NotificationReads.Count());
    }
}
