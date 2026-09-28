using System.Security.Claims;
using System.Text.Json;
using LendingService.Data;
using LendingService.Models;
using LendingService.Kafka;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LendingService.Controllers;

[ApiController]
[Route("api/my-borrowings")]
[Authorize(Roles = "User")]
public sealed class MyBorrowingsController(LendingDbContext db, IHttpClientFactory clients, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        // Identity always comes from the validated token, never a request parameter.
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var userId) || userId <= 0)
            return Unauthorized(new { message = "Invalid user identity. Please log in again." });
        var loans = await db.BorrowRecords.AsNoTracking().Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CheckoutDate).ThenByDescending(b => b.Id).ToListAsync(cancellationToken);
        var fines = await db.Fines.AsNoTracking()
            .Where(f => db.BorrowRecords.Any(b => b.Id == f.BorrowRecordId && b.UserId == userId))
            .ToDictionaryAsync(f => f.BorrowRecordId, cancellationToken);
        var pending = await db.Reservations.AsNoTracking()
            .Where(r => r.UserId == userId && (r.Status == "Pending" || r.Status == "Accepting"))
            .OrderBy(r => r.ReservationDate).ThenBy(r => r.Id).ToListAsync(cancellationToken);
        var titles = new Dictionary<int, string>();
        using var client = clients.CreateClient();
        foreach (var bookId in loans.Select(b => b.BookId).Concat(pending.Select(r => r.BookId)).Distinct())
        {
            titles[bookId] = $"Book #{bookId} (title unavailable)";
            if (!Uri.TryCreate(configuration["InventoryService:BaseUrl"], UriKind.Absolute, out var inventoryUrl)) continue;
            try
            {
                using var response = await client.GetAsync(new Uri(inventoryUrl, $"/api/Books/{bookId}"), cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var book = await response.Content.ReadFromJsonAsync<BookTitle>(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(book?.Title)) titles[bookId] = book.Title;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException ||
                (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // A catalogue outage must not hide the user's loans or financial records.
            }
        }
        var now = DateTime.UtcNow;
        var records = loans.Select(b =>
        {
            fines.TryGetValue(b.Id, out var fine);
            var days = b.ReturnDate == null
                ? ReturnFineCalculator.DaysOverdue(b.DueDate, b.ReturnRequestedAtUtc ?? now)
                : fine?.DaysOverdue ?? ReturnFineCalculator.DaysOverdue(b.DueDate, b.ReturnDate.Value);
            return new {
                b.Id, b.BookId, bookTitle = titles[b.BookId],
                checkoutDate = DateTime.SpecifyKind(b.CheckoutDate, DateTimeKind.Utc),
                dueDate = DateTime.SpecifyKind(b.DueDate, DateTimeKind.Utc),
                returnDate = b.ReturnDate.HasValue ? (DateTime?)DateTime.SpecifyKind(b.ReturnDate.Value, DateTimeKind.Utc) : null,
                status = b.ReturnDate != null ? "Returned" : b.ReturnRequestedAtUtc != null ? "Return pending" : "Borrowed",
                daysOverdue = days,
                fineAmount = b.ReturnDate == null ? days * ReturnFineCalculator.DailyRate : fine?.Amount ?? 0m,
                fineStatus = b.ReturnDate == null ? "Estimated" : fine?.Status ?? "No fine"
            };
        }).ToList();
        return Ok(new {
            pending = pending.Select(r => new { r.Id, r.BookId, bookTitle = titles[r.BookId],
                reservationDate = DateTime.SpecifyKind(r.ReservationDate, DateTimeKind.Utc),
                r.Status, canCancel = r.Status == "Pending" }),
            active = records.Where(b => b.returnDate == null).OrderBy(b => b.dueDate).ThenBy(b => b.Id),
            history = records.Where(b => b.returnDate != null).OrderByDescending(b => b.returnDate).ThenByDescending(b => b.Id),
            totalUnpaid = fines.Values.Where(f => f.Status == "Unpaid").Sum(f => f.Amount),
            estimatedActiveFines = records.Where(b => b.returnDate == null).Sum(b => b.fineAmount)
        });
    }

    private sealed class BookTitle { public string? Title { get; set; } }

    [HttpPost("reservations/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var userId) || userId <= 0)
            return Unauthorized(new { message = "Invalid user identity. Please log in again." });
        var reservation = await db.Reservations.SingleOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken);
        if (reservation == null) return NotFound(new { message = "Reservation not found." });
        if (reservation.Status == "Cancelled") return Ok(new { message = "Reservation already cancelled." });
        if (reservation.Status != "Pending")
            return Conflict(new { message = "This reservation is already being processed or has been accepted and cannot be cancelled. Refresh your list." });
        reservation.Status = "Cancelled";
        var evt = new ReservationCancelledEvent { ReservationId = reservation.Id, BookId = reservation.BookId,
            UserId = userId, ReservationDate = DateTime.SpecifyKind(reservation.ReservationDate, DateTimeKind.Utc) };
        db.ReservationEvents.Add(new ReservationEventOutbox { Id = evt.EventId, ReservationId = reservation.Id,
            EventType = evt.EventType, CreatedAtUtc = evt.TimestampUtc,
            Payload = JsonSerializer.Serialize(evt, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        try
        {
            // Status concurrency checks and the transaction prevent both cancellation and acceptance winning.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var status = await db.Reservations.AsNoTracking().Where(r => r.Id == id && r.UserId == userId)
                .Select(r => r.Status).SingleOrDefaultAsync(cancellationToken);
            if (status == "Cancelled") return Ok(new { message = "Reservation already cancelled." });
            if (status != "Pending") return Conflict(new { message = "The reservation changed while you were cancelling it. Refresh your list." });
            throw;
        }
        return Ok(new { message = "Reservation cancelled. Copy counts are unchanged." });
    }
}
