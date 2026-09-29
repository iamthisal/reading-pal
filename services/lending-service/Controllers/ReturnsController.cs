using System.Text.Json;
using LendingService.Data;
using LendingService.Kafka;
using LendingService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LendingService.Controllers;

[ApiController]
[Route("api/returns")]
[Authorize(Roles = "Admin")]
public sealed class ReturnsController(LendingDbContext db, IHttpClientFactory clients, IConfiguration configuration) : ControllerBase
{
    [HttpPost("{borrowRecordId:int}")]
    public async Task<IActionResult> Return(int borrowRecordId, CancellationToken cancellationToken)
    {
        var loan = await db.BorrowRecords.SingleOrDefaultAsync(b => b.Id == borrowRecordId, cancellationToken);
        if (loan == null) return NotFound(new { message = "Borrow record not found." });
        if (loan.ReturnDate != null) return await Result(loan, cancellationToken);
        var reservation = await db.Reservations.SingleAsync(r => r.Id == loan.ReservationId, cancellationToken);
        if (reservation.Status != "Borrowed" && reservation.Status != "Returning")
            return Conflict(new { message = "Only borrowed books can be returned." });
        if (!Uri.TryCreate(configuration["InventoryService:BaseUrl"], UriKind.Absolute, out var inventoryUrl))
            return StatusCode(500, new { message = "Inventory Service configuration is missing." });

        if (reservation.Status == "Borrowed")
        {
            // Preserve the counter return time, even if Inventory is down and completion needs a retry.
            loan.ReturnRequestedAtUtc = DateTime.UtcNow;
            reservation.Status = "Returning";
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "This return is being processed. Refresh and retry." });
            }
        }

        try
        {
            using var client = clients.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post,
                new Uri(inventoryUrl, $"/api/Books/{loan.BookId}/checkouts/{loan.ReservationId}/return"));
            request.Headers.TryAddWithoutValidation("Authorization", Request.Headers.Authorization.ToString());
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException ||
            (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return StatusCode(503, new { message = "Inventory return could not be confirmed. Retry Return; the original return date is preserved." });
        }

        loan.ReturnDate = loan.ReturnRequestedAtUtc!.Value;
        reservation.ReturnDate = loan.ReturnDate;
        reservation.Status = "Returned";
        var days = ReturnFineCalculator.DaysOverdue(loan.DueDate, loan.ReturnDate.Value);
        if (days > 0)
            db.Fines.Add(new Fine { BorrowRecordId = loan.Id, DaysOverdue = days,
                DailyRate = ReturnFineCalculator.DailyRate, Amount = days * ReturnFineCalculator.DailyRate });
        var returnedEvent = new BookReturnedEvent { ReservationId = reservation.Id,
            BorrowRecordId = loan.Id, BookId = loan.BookId, UserId = loan.UserId,
            ReturnDate = DateTime.SpecifyKind(loan.ReturnDate.Value, DateTimeKind.Utc) };
        db.ReservationEvents.Add(new ReservationEventOutbox { Id = returnedEvent.EventId,
            ReservationId = reservation.Id, EventType = returnedEvent.EventType,
            CreatedAtUtc = returnedEvent.TimestampUtc,
            Payload = JsonSerializer.Serialize(returnedEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            // A competing return may have won the concurrency or unique fine/event checks.
            db.ChangeTracker.Clear();
            var completed = await db.BorrowRecords.AsNoTracking().SingleAsync(b => b.Id == borrowRecordId, cancellationToken);
            if (completed.ReturnDate != null) return await Result(completed, cancellationToken);
            return StatusCode(503, new { message = "Return could not be saved. Retry Return to safely complete it." });
        }
        return await Result(loan, cancellationToken);
    }

    private async Task<IActionResult> Result(BorrowRecord loan, CancellationToken cancellationToken)
    {
        var fine = await db.Fines.AsNoTracking().SingleOrDefaultAsync(f => f.BorrowRecordId == loan.Id, cancellationToken);
        return Ok(new { borrowRecordId = loan.Id, returnDate = DateTime.SpecifyKind(loan.ReturnDate!.Value, DateTimeKind.Utc),
            status = "Returned", daysOverdue = fine?.DaysOverdue ?? 0, fineAmount = fine?.Amount ?? 0m,
            fineStatus = fine?.Status, message = "Book returned. Return event queued for Kafka." });
    }
}
