using System.Text.Json;
using LendingService.Kafka;
using LendingService.Models;
using LendingService.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace LendingService.Tests.Kafka;

public class ReservationSnapshotServiceTests
{
    [Fact]
    public async Task QueueSnapshotsAsync_QueuesOneSnapshotPerActiveReservationOnly()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var statuses = new[] { "Pending", "Accepting", "Borrowed", "Returning", "Returned", "Cancelled" };
        for (var i = 0; i < statuses.Length; i++)
            context.Reservations.Add(new Reservation { Id = i + 1, UserId = 7, BookId = 20 + i, Status = statuses[i], ReservationDate = DateTime.UtcNow.AddDays(-3) });
        await context.SaveChangesAsync();

        var queued = await new ReservationSnapshotService(context, NullLogger<ReservationSnapshotService>.Instance)
            .QueueSnapshotsAsync(CancellationToken.None);

        Assert.Equal(4, queued);
        var snapshots = context.ReservationEvents.Where(e => e.EventType == "reservation-snapshot").ToList();
        Assert.Equal(new[] { 1, 2, 3, 4 }, snapshots.Select(e => e.ReservationId).OrderBy(id => id));
        using var payload = JsonDocument.Parse(snapshots.Single(e => e.ReservationId == 3).Payload);
        Assert.Equal("Borrowed", payload.RootElement.GetProperty("status").GetString());
        Assert.Equal(7, payload.RootElement.GetProperty("userId").GetInt32());
        Assert.Equal(22, payload.RootElement.GetProperty("bookId").GetInt32());
    }

    [Fact]
    public async Task QueueSnapshotsAsync_RunningAgain_DoesNotQueueDuplicates()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, UserId = 7, BookId = 20, Status = "Pending" });
        await context.SaveChangesAsync();
        var service = new ReservationSnapshotService(context, NullLogger<ReservationSnapshotService>.Instance);

        Assert.Equal(1, await service.QueueSnapshotsAsync(CancellationToken.None));
        Assert.Equal(0, await service.QueueSnapshotsAsync(CancellationToken.None));
        Assert.Single(context.ReservationEvents);
    }
}
