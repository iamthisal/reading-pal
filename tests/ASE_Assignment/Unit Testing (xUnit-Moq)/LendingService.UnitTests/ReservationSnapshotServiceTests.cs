using LendingService.Kafka;
using Microsoft.Extensions.Logging;
using Moq;

namespace LendingService.UnitTests;

public class ReservationSnapshotServiceTests
{
    [Theory]
    [InlineData("Pending", 1)]
    [InlineData("Accepting", 1)]
    [InlineData("Borrowed", 1)]
    [InlineData("Returning", 1)]
    [InlineData("Returned", 0)]
    [InlineData("Cancelled", 0)]
    // Proves snapshots include active reservations and exclude completed ones.
    public async Task QueueSnapshotsAsync_ReservationStatus_QueuesOnlyActive(string status, int expected)
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        db.Reservations.Add(LendingTestData.Reservation(status));
        await db.SaveChangesAsync();
        var service = new ReservationSnapshotService(db, new Mock<ILogger<ReservationSnapshotService>>().Object);
        // Act
        var count = await service.QueueSnapshotsAsync(CancellationToken.None);
        // Assert
        Assert.Equal(expected, count);
        Assert.Equal(expected, db.ReservationEvents.Count());
    }

    [Fact]
    // Proves an existing snapshot prevents duplicate snapshot events.
    public async Task QueueSnapshotsAsync_ExistingSnapshot_QueuesNothing()
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        db.Reservations.Add(LendingTestData.Reservation());
        await db.SaveChangesAsync();
        var service = new ReservationSnapshotService(db, new Mock<ILogger<ReservationSnapshotService>>().Object);
        await service.QueueSnapshotsAsync(CancellationToken.None);
        // Act
        var count = await service.QueueSnapshotsAsync(CancellationToken.None);
        // Assert
        Assert.Equal(0, count);
        Assert.Equal(1, db.ReservationEvents.Count());
    }
}
