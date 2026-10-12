using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;
using NotificationService.Tests.TestSupport;

namespace NotificationService.Tests.Services;

/// <summary>
/// Issue #90: book-deleted and reservation events travel on different Kafka topics, so the deletion can
/// be handled before the reservation it affects is known. The customer must still be told.
/// </summary>
public class DeletionBeforeReservationTests
{
    private static string BookDeleted(Guid? eventId = null, int bookId = 12, string title = "Clean Code") => TestFactory.EventJson(new
    {
        EventId = eventId ?? Guid.NewGuid(), EventType = "book-deleted", TimestampUtc = DateTime.UtcNow, BookId = bookId,
        Book = new { Id = bookId, Title = title, Author = "Robert C. Martin", Isbn = "978", Genre = "Software", TotalCopies = 2, AvailableCopies = 2 }
    });

    private static string Reservation(string type, int reservationId, int userId, int bookId = 12, string? status = null) => TestFactory.EventJson(new
    {
        EventId = Guid.NewGuid(), EventType = type, SchemaVersion = 2, TimestampUtc = DateTime.UtcNow,
        ReservationId = reservationId, UserId = userId, BookId = bookId, ReservationDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(14), ReturnDate = DateTime.UtcNow, Status = status
    });

    private static List<Notification> DeletionNotices(NotificationDbContext db) =>
        db.Notifications.Where(n => n.Type == NotificationTypes.BookDeleted).ToList();

    [Fact]
    public async Task ReservationCreatedAfterTheDeletion_StillNotifiesTheCustomer()
    {
        using var db = TestFactory.CreateDbContext();
        var deletionId = Guid.NewGuid();

        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookDeleted(deletionId), CancellationToken.None);
        Assert.Empty(DeletionNotices(db));

        await TestFactory.CreateHandler(db).HandleAsync(Reservation("reservation-created", 1, userId: 7), CancellationToken.None);

        var notice = Assert.Single(DeletionNotices(db));
        Assert.Equal(7, notice.UserId);
        Assert.Equal(NotificationAudiences.User, notice.Audience);
        Assert.Equal(deletionId, notice.EventId);
        Assert.Equal("Clean Code", notice.BookTitle);
        Assert.Contains("'Clean Code'", notice.Message);
    }

    [Fact]
    public async Task LateEventsForTheSameReservation_NotifyOnlyOnce()
    {
        using var db = TestFactory.CreateDbContext();
        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookDeleted(), CancellationToken.None);
        var handler = TestFactory.CreateHandler(db);
        var created = Reservation("reservation-created", 1, userId: 7);

        await handler.HandleAsync(created, CancellationToken.None);
        await handler.HandleAsync(created, CancellationToken.None);
        await handler.HandleAsync(Reservation("reservation-accepted", 1, userId: 7), CancellationToken.None);

        Assert.Single(DeletionNotices(db));
    }

    [Fact]
    public async Task UsualOrder_ReservationThenDeletion_NotifiesOnce_EvenIfMoreEventsFollow()
    {
        using var db = TestFactory.CreateDbContext();
        var handler = TestFactory.CreateHandler(db);
        await handler.HandleAsync(Reservation("reservation-created", 1, userId: 7), CancellationToken.None);

        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookDeleted(), CancellationToken.None);
        await handler.HandleAsync(Reservation("reservation-accepted", 1, userId: 7), CancellationToken.None);

        Assert.Single(DeletionNotices(db));
    }

    [Fact]
    public async Task EachCustomerWithALateReservation_IsNotified()
    {
        using var db = TestFactory.CreateDbContext();
        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookDeleted(), CancellationToken.None);
        var handler = TestFactory.CreateHandler(db);

        await handler.HandleAsync(Reservation("reservation-created", 1, userId: 7), CancellationToken.None);
        await handler.HandleAsync(Reservation("reservation-created", 2, userId: 8), CancellationToken.None);

        Assert.Equal(new[] { 7, 8 }, DeletionNotices(db).Select(n => n.UserId).OrderBy(id => id));
    }

    [Theory]
    [InlineData("reservation-cancelled")]
    [InlineData("book-returned")]
    public async Task ALateEventThatClosesTheReservation_NotifiesNobody(string closingEvent)
    {
        using var db = TestFactory.CreateDbContext();
        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookDeleted(), CancellationToken.None);

        await TestFactory.CreateHandler(db).HandleAsync(Reservation(closingEvent, 1, userId: 7), CancellationToken.None);

        Assert.Empty(DeletionNotices(db));
    }

    [Fact]
    public async Task ALateReservationForADifferentBook_NotifiesNobody()
    {
        using var db = TestFactory.CreateDbContext();
        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookDeleted(bookId: 12), CancellationToken.None);

        await TestFactory.CreateHandler(db).HandleAsync(Reservation("reservation-created", 1, userId: 7, bookId: 13), CancellationToken.None);

        Assert.Empty(DeletionNotices(db));
    }

    [Fact]
    public async Task ALateSnapshotOfAnOpenReservation_NotifiesTheCustomer()
    {
        using var db = TestFactory.CreateDbContext();
        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookDeleted(), CancellationToken.None);

        await TestFactory.CreateHandler(db).HandleAsync(Reservation("reservation-snapshot", 1, userId: 7, status: "Borrowed"), CancellationToken.None);

        Assert.Equal(7, Assert.Single(DeletionNotices(db)).UserId);
    }

    [Fact]
    public async Task TheDeletionIsRememberedOnce_WhenTheEventIsRedelivered()
    {
        using var db = TestFactory.CreateDbContext();
        var handler = TestFactory.CreateCatalogHandler(db);
        var json = BookDeleted();

        await handler.HandleAsync(json, CancellationToken.None);
        await handler.HandleAsync(json, CancellationToken.None);

        var remembered = Assert.Single(db.DeletedBooks);
        Assert.Equal(12, remembered.BookId);
        Assert.Equal("Clean Code", remembered.Title);
    }
}
