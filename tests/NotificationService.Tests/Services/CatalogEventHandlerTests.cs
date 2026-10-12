using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;
using NotificationService.Tests.TestSupport;

namespace NotificationService.Tests.Services;

public class CatalogEventHandlerTests
{
    private static readonly DateTime AddedAt = new(2026, 10, 8, 5, 0, 0, DateTimeKind.Utc);

    // The shape Inventory publishes: the full book is nested under "book".
    private static string BookEvent(string type, Guid? eventId = null, int bookId = 12, string title = "Clean Code") => TestFactory.EventJson(new
    {
        EventId = eventId ?? Guid.NewGuid(),
        EventType = type,
        TimestampUtc = AddedAt,
        BookId = bookId,
        Book = new { Id = bookId, Title = title, Author = "Robert C. Martin", Isbn = "978", Genre = "Software", TotalCopies = 2, AvailableCopies = 2 }
    });

    private static string ReservationEvent(string type, int reservationId, int userId, int bookId, string? cancelledBy = null) => TestFactory.EventJson(new
    {
        EventId = Guid.NewGuid(), EventType = type, SchemaVersion = 2, TimestampUtc = DateTime.UtcNow,
        ReservationId = reservationId, UserId = userId, BookId = bookId, ReservationDate = DateTime.UtcNow,
        CancelledBy = cancelledBy, DueDate = DateTime.UtcNow.AddDays(14), ReturnDate = DateTime.UtcNow
    });

    private static async Task ApplyAsync(NotificationDbContext db, params string[] lendingEvents)
    {
        var handler = TestFactory.CreateHandler(db);
        foreach (var json in lendingEvents) await handler.HandleAsync(json, CancellationToken.None);
    }

    [Fact]
    public async Task BookCreated_StoresOneAnnouncementWithTitleAtTheTimeTheBookWasAdded()
    {
        using var db = TestFactory.CreateDbContext();

        var outcome = await TestFactory.CreateCatalogHandler(db).HandleAsync(BookEvent("book-created"), CancellationToken.None);

        Assert.Equal(HandleOutcome.Created, outcome);
        var announcement = Assert.Single(db.Notifications, n => n.Audience == NotificationAudiences.Customers);
        Assert.Equal(NotificationTypes.NewBook, announcement.Type);
        Assert.Equal("New in the catalogue: 'Clean Code' by Robert C. Martin.", announcement.Message);
        Assert.Equal("Clean Code", announcement.BookTitle);
        Assert.Equal(AddedAt, announcement.CreatedAtUtc);
    }

    [Fact]
    public async Task BookCreated_ReceivedTwice_CreatesOneAnnouncement()
    {
        using var db = TestFactory.CreateDbContext();
        var handler = TestFactory.CreateCatalogHandler(db);
        var json = BookEvent("book-created");

        await handler.HandleAsync(json, CancellationToken.None);
        Assert.Equal(HandleOutcome.Duplicate, await handler.HandleAsync(json, CancellationToken.None));
        Assert.Single(db.Notifications, n => n.Audience == NotificationAudiences.Customers);
        Assert.Single(db.Notifications, n => n.Audience == NotificationAudiences.Admin);
    }

    [Fact]
    public async Task BookDeleted_NotifiesOnlyCustomersWithPendingReservationOrActiveBorrowing()
    {
        using var db = TestFactory.CreateDbContext();
        await ApplyAsync(db,
            ReservationEvent("reservation-created", 1, userId: 7, bookId: 12),        // pending
            ReservationEvent("reservation-created", 2, userId: 8, bookId: 12),
            ReservationEvent("reservation-accepted", 2, userId: 8, bookId: 12),       // borrowing
            ReservationEvent("reservation-created", 3, userId: 9, bookId: 12),
            ReservationEvent("reservation-cancelled", 3, userId: 9, bookId: 12, "User"), // cancelled: unrelated now
            ReservationEvent("reservation-accepted", 4, userId: 10, bookId: 12),
            ReservationEvent("book-returned", 4, userId: 10, bookId: 12),            // returned: unrelated now
            ReservationEvent("reservation-created", 5, userId: 11, bookId: 99));     // a different book
        var before = db.Notifications.Select(n => n.Id).ToList();

        var outcome = await TestFactory.CreateCatalogHandler(db).HandleAsync(BookEvent("book-deleted", title: "Clean Code"), CancellationToken.None);

        Assert.Equal(HandleOutcome.Created, outcome);
        var notices = db.Notifications.Where(n => !before.Contains(n.Id) && n.Audience == NotificationAudiences.User).ToList();
        Assert.Equal(new[] { 7, 8 }, notices.Select(n => n.UserId).OrderBy(id => id));
        Assert.All(notices, n =>
        {
            Assert.Equal(NotificationAudiences.User, n.Audience);
            Assert.Equal(NotificationTypes.BookDeleted, n.Type);
            Assert.Equal("Clean Code", n.BookTitle);
            Assert.Contains("'Clean Code'", n.Message);
            // The deletion does not cancel anything, so the message must not say it did.
            Assert.DoesNotContain("cancel", n.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fine", n.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task BookDeleted_WithNoConnectedCustomers_NotifiesNobody()
    {
        using var db = TestFactory.CreateDbContext();
        await ApplyAsync(db, ReservationEvent("reservation-created", 1, userId: 7, bookId: 99));
        var before = db.Notifications.Count(n => n.Audience == NotificationAudiences.User);

        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookEvent("book-deleted", bookId: 12), CancellationToken.None);

        // No customer is told; only the admin notice about the deletion is created.
        Assert.Equal(before, db.Notifications.Count(n => n.Audience == NotificationAudiences.User));
    }

    [Fact]
    public async Task BookDeleted_ReceivedTwice_CreatesNoDuplicates()
    {
        using var db = TestFactory.CreateDbContext();
        await ApplyAsync(db, ReservationEvent("reservation-created", 1, userId: 7, bookId: 12),
            ReservationEvent("reservation-created", 2, userId: 8, bookId: 12));
        var handler = TestFactory.CreateCatalogHandler(db);
        var json = BookEvent("book-deleted");

        await handler.HandleAsync(json, CancellationToken.None);
        Assert.Equal(HandleOutcome.Duplicate, await handler.HandleAsync(json, CancellationToken.None));
        Assert.Equal(2, db.Notifications.Count(n => n.Type == NotificationTypes.BookDeleted));
    }

    [Fact]
    public async Task ReservationState_LateCreatedEventDoesNotReopenCancelledReservation()
    {
        // Created and cancelled travel on different topics, so the cancellation can be consumed first.
        using var db = TestFactory.CreateDbContext();
        await ApplyAsync(db,
            ReservationEvent("reservation-cancelled", 1, userId: 7, bookId: 12, "User"),
            ReservationEvent("reservation-created", 1, userId: 7, bookId: 12));

        Assert.Equal(ReservationStatuses.Closed, Assert.Single(db.ReservationStates).Status);
        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookEvent("book-deleted"), CancellationToken.None);
        Assert.DoesNotContain(db.Notifications, n => n.Type == NotificationTypes.BookDeleted);
    }

    [Fact]
    public async Task ReservationState_AcceptedWithoutCreatedEvent_CountsAsBorrowing()
    {
        // Reservations made before reservation-created existed only ever had an accepted event.
        using var db = TestFactory.CreateDbContext();
        await ApplyAsync(db, ReservationEvent("reservation-accepted", 1, userId: 7, bookId: 12));

        Assert.Equal(ReservationStatuses.Borrowed, Assert.Single(db.ReservationStates).Status);
    }
}
