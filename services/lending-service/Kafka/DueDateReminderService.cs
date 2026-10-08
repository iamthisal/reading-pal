using System.Text.Json;
using LendingService.Data;
using LendingService.Models;
using Microsoft.EntityFrameworkCore;

namespace LendingService.Kafka;

/// <summary>
/// Queues one book-due-soon event per active loan that is due within the next two Sri Lankan calendar
/// days. A loan that is returned or awaiting return confirmation is skipped. Loans due today or tomorrow
/// that never got a reminder (for example because earlier checks failed) still get exactly one, with the
/// actual due date.
/// </summary>
public sealed class DueDateReminderService(LendingDbContext db, ILogger<DueDateReminderService> logger)
{
    public const int ReminderDaysBeforeDue = 2;

    public async Task<int> QueueRemindersAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Narrow in SQL by a generous UTC window, then decide exactly by calendar day in Asia/Colombo.
        var from = nowUtc.AddDays(-2);
        var to = nowUtc.AddDays(ReminderDaysBeforeDue + 2);
        var candidates = await db.BorrowRecords
            .Where(b => b.ReturnDate == null && b.ReturnRequestedAtUtc == null && b.DueReminderSentAtUtc == null
                && b.DueDate >= from && b.DueDate <= to)
            .OrderBy(b => b.DueDate).ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);

        var queued = 0;
        foreach (var loan in candidates)
        {
            var daysUntilDue = ReturnFineCalculator.DaysUntilDue(loan.DueDate, nowUtc);
            if (daysUntilDue is < 0 or > ReminderDaysBeforeDue) continue;

            var reminder = new BookDueSoonEvent
            {
                ReservationId = loan.ReservationId,
                BorrowRecordId = loan.Id,
                BookId = loan.BookId,
                UserId = loan.UserId,
                DueDate = DateTime.SpecifyKind(loan.DueDate, DateTimeKind.Utc),
                DaysUntilDue = daysUntilDue,
                TimestampUtc = nowUtc
            };
            loan.DueReminderSentAtUtc = nowUtc;
            db.ReservationEvents.Add(new ReservationEventOutbox
            {
                Id = reminder.EventId,
                ReservationId = loan.ReservationId,
                EventType = reminder.EventType,
                CreatedAtUtc = nowUtc,
                Payload = JsonSerializer.Serialize(reminder, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });
            try
            {
                // The reminder flag and its event commit together: a loan is either reminded once or not at all.
                await db.SaveChangesAsync(cancellationToken);
                queued++;
            }
            catch (DbUpdateException ex)
            {
                // Another run already reminded this loan (unique outbox index); move on to the next one.
                db.ChangeTracker.Clear();
                logger.LogWarning(ex, "Skipping due-date reminder for borrow record {BorrowRecordId}; it was already queued.", loan.Id);
            }
        }
        return queued;
    }
}
