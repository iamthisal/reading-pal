using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Models;
using NotificationService.Services;
using NotificationService.Tests.TestSupport;

namespace NotificationService.Tests.Services;

// Review findings on #83: backfilled reservations, and state transitions surviving save conflicts.
public class ReservationStateReviewTests
{
    private static string Snapshot(int reservationId, int userId, int bookId, string status) => TestFactory.EventJson(new
    {
        EventId = Guid.NewGuid(), EventType = "reservation-snapshot", SchemaVersion = 1, TimestampUtc = DateTime.UtcNow,
        ReservationId = reservationId, UserId = userId, BookId = bookId, Status = status, ReservationDate = DateTime.UtcNow.AddDays(-10)
    });

    private static string Event(string type, int reservationId, int userId = 7, int bookId = 12, string? cancelledBy = null) => TestFactory.EventJson(new
    {
        EventId = Guid.NewGuid(), EventType = type, SchemaVersion = 2, TimestampUtc = DateTime.UtcNow,
        ReservationId = reservationId, UserId = userId, BookId = bookId, ReservationDate = DateTime.UtcNow,
        CancelledBy = cancelledBy, DueDate = DateTime.UtcNow.AddDays(14), ReturnDate = DateTime.UtcNow
    });

    private static string BookDeleted(int bookId = 12) => TestFactory.EventJson(new
    {
        EventId = Guid.NewGuid(), EventType = "book-deleted", TimestampUtc = DateTime.UtcNow, BookId = bookId,
        Book = new { Id = bookId, Title = "Clean Code" }
    });

    [Theory]
    [InlineData("Pending", "Pending")]
    [InlineData("Accepting", "Pending")]
    [InlineData("Borrowed", "Borrowed")]
    [InlineData("Returning", "Borrowed")]
    public async Task Snapshot_RecordsActiveReservationWithoutNotifyingAnyone(string lendingStatus, string expected)
    {
        using var db = TestFactory.CreateDbContext();

        var outcome = await TestFactory.CreateHandler(db).HandleAsync(Snapshot(1, 7, 12, lendingStatus), CancellationToken.None);

        Assert.Equal(HandleOutcome.Skipped, outcome);
        Assert.Equal(expected, Assert.Single(db.ReservationStates).Status);
        Assert.Empty(db.Notifications);
    }

    [Fact]
    public async Task BackfilledReservation_IsNotifiedWhenItsBookIsDeleted()
    {
        // A reservation made before reservation-created existed: only Lending's snapshot tells us about it.
        using var db = TestFactory.CreateDbContext();
        await TestFactory.CreateHandler(db).HandleAsync(Snapshot(1, 7, 12, "Pending"), CancellationToken.None);

        var outcome = await TestFactory.CreateCatalogHandler(db).HandleAsync(BookDeleted(), CancellationToken.None);

        Assert.Equal(HandleOutcome.Created, outcome);
        Assert.Equal(7, Assert.Single(db.Notifications).UserId);
    }

    [Fact]
    public async Task OlderSnapshot_DoesNotReopenReservationClosedByNewerEvent()
    {
        using var db = TestFactory.CreateDbContext();
        var handler = TestFactory.CreateHandler(db);
        await handler.HandleAsync(Event("reservation-cancelled", 1, cancelledBy: "User"), CancellationToken.None);

        await handler.HandleAsync(Snapshot(1, 7, 12, "Pending"), CancellationToken.None);

        Assert.Equal(ReservationStatuses.Closed, Assert.Single(db.ReservationStates).Status);
    }

    [Fact]
    public async Task ConcurrentInsert_TransitionIsReappliedToTheStoredRow()
    {
        // Another delivery inserts the reservation as Pending between our read and our save. Our event
        // closes it, and that must not be thrown away.
        using var database = new SqliteTestDatabase();
        var interceptor = new BeforeFirstSaveInterceptor(async () =>
        {
            using var other = database.CreateContext();
            other.ReservationStates.Add(new ReservationState { ReservationId = 1, UserId = 7, BookId = 12, Status = ReservationStatuses.Pending });
            await other.SaveChangesAsync();
        });
        using var db = database.CreateContext(interceptor);

        await TestFactory.CreateHandler(db).HandleAsync(Event("reservation-cancelled", 1, cancelledBy: "User"), CancellationToken.None);

        using var check = database.CreateContext();
        Assert.Equal(ReservationStatuses.Closed, Assert.Single(check.ReservationStates).Status);
    }

    [Fact]
    public async Task UnrelatedSaveFailure_PropagatesSoKafkaRedeliversTheMessage()
    {
        var name = Guid.NewGuid().ToString();
        var interceptor = new BeforeFirstSaveInterceptor(() => throw new DbUpdateException("Simulated database failure."));
        using var db = TestFactory.CreateDbContext(name, interceptor);
        var handler = new LendingEventHandler(db, new FixedTitleLookup("Clean Code"), NullLogger<LendingEventHandler>.Instance);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            handler.HandleAsync(Event("reservation-created", 1), CancellationToken.None));

        using var check = TestFactory.CreateDbContext(name);
        Assert.Empty(check.ReservationStates);
        Assert.Empty(check.Notifications);
    }
}
