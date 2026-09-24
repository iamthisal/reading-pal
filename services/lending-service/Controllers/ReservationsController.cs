using System.Security.Claims;
using LendingService.Data;
using LendingService.DTOs;
using LendingService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;
using LendingService.Kafka;

namespace LendingService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReservationsController : ControllerBase
    {
        private readonly LendingDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public ReservationsController(LendingDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<PendingReservationResponse>>> GetPending(CancellationToken cancellationToken)
        {
            var reservations = await _context.Reservations.AsNoTracking()
                .Where(r => r.Status == "Pending")
                .OrderBy(r => r.ReservationDate)
                .ThenBy(r => r.Id)
                .ToListAsync(cancellationToken);

            if (reservations.Count == 0)
                return Ok(Array.Empty<PendingReservationResponse>());

            var (names, titles, lookupError) = await LookupDetails(reservations.Select(r => r.BookId), cancellationToken);
            if (lookupError != null) return lookupError;

            return Ok(reservations.Select(r => new PendingReservationResponse
            {
                Id = r.Id,
                UserId = r.UserId,
                BookId = r.BookId,
                UserName = names.GetValueOrDefault(r.UserId, $"User unavailable (#{r.UserId})"),
                BookTitle = titles[r.BookId],
                Status = r.Status,
                // MySQL datetime values have no Kind; reservations are written in UTC.
                ReservationDate = DateTime.SpecifyKind(r.ReservationDate, DateTimeKind.Utc)
            }).ToList());
        }

        private async Task<(Dictionary<int, string> Names, Dictionary<int, string> Titles, ObjectResult? Error)> LookupDetails(IEnumerable<int> bookIds, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(_configuration["UserService:BaseUrl"], UriKind.Absolute, out var userBaseUrl)
                || !Uri.TryCreate(_configuration["InventoryService:BaseUrl"], UriKind.Absolute, out var inventoryBaseUrl))
                return (new(), new(), StatusCode(500, new { message = "Reservation lookup service configuration is missing or invalid." }));

            using var client = _httpClientFactory.CreateClient();
            var names = new Dictionary<int, string>();
            var titles = new Dictionary<int, string>();
            try
            {
                foreach (var group in new[] { "active", "pending" })
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(userBaseUrl, $"/api/admin/users/{group}"));
                    request.Headers.TryAddWithoutValidation("Authorization", Request.Headers.Authorization.ToString());
                    using var response = await client.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    var users = await response.Content.ReadFromJsonAsync<List<UserNameResponse>>(cancellationToken)
                        ?? throw new JsonException("Missing user response.");
                    foreach (var user in users)
                        names[user.Id] = $"{user.FirstName} {user.LastName}".Trim();
                }

                foreach (var bookId in bookIds.Distinct())
                {
                    using var response = await client.GetAsync(new Uri(inventoryBaseUrl, $"/api/Books/{bookId}"), cancellationToken);
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        titles[bookId] = $"Deleted book (#{bookId})";
                        continue;
                    }
                    response.EnsureSuccessStatusCode();
                    var book = await response.Content.ReadFromJsonAsync<BookTitleResponse>(cancellationToken);
                    titles[bookId] = book?.Title ?? throw new JsonException("Missing book response.");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException
                || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                return (names, titles, StatusCode(503, new { message = "Unable to load reservation details from User or Inventory Service. Please try again." }));
            }

            return (names, titles, null);
        }

        [HttpGet("borrowed")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<BorrowRecordResponse>>> GetBorrowed(CancellationToken cancellationToken)
        {
            var records = await _context.BorrowRecords.AsNoTracking()
                .Where(b => b.ReturnDate == null)
                .OrderBy(b => b.DueDate).ThenBy(b => b.Id)
                .ToListAsync(cancellationToken);
            if (records.Count == 0) return Ok(Array.Empty<BorrowRecordResponse>());

            var (names, titles, lookupError) = await LookupDetails(records.Select(b => b.BookId), cancellationToken);
            if (lookupError != null) return lookupError;

            var now = DateTime.UtcNow;
            return Ok(records.Select(b => new BorrowRecordResponse
            {
                Id = b.Id,
                ReservationId = b.ReservationId,
                UserId = b.UserId,
                BookId = b.BookId,
                UserName = names.GetValueOrDefault(b.UserId, $"User unavailable (#{b.UserId})"),
                BookTitle = titles[b.BookId],
                CheckoutDate = DateTime.SpecifyKind(b.CheckoutDate, DateTimeKind.Utc),
                DueDate = DateTime.SpecifyKind(b.DueDate, DateTimeKind.Utc),
                IsOverdue = b.DueDate < now
            }).ToList());
        }

        private sealed class UserNameResponse
        {
            public int Id { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
        }

        private sealed class BookTitleResponse
        {
            public string? Title { get; set; }
        }

        [HttpPost("{id:int}/accept")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Accept(int id, CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (reservation == null)
                return NotFound(new { message = "Reservation not found." });
            if (reservation.Status != "Pending")
                return Conflict(new { message = "Only pending reservations can be accepted. Refresh the queue." });

            // Match existing Lending timestamps: store UTC and retain the checkout time.
            var checkoutDate = DateTime.UtcNow;
            var dueDate = checkoutDate.AddDays(14);
            var borrowRecord = new BorrowRecord
            {
                ReservationId = reservation.Id,
                BookId = reservation.BookId,
                UserId = reservation.UserId,
                CheckoutDate = checkoutDate,
                DueDate = dueDate
            };
            _context.BorrowRecords.Add(borrowRecord);
            var acceptedEvent = new ReservationAcceptedEvent
            {
                ReservationId = reservation.Id,
                UserId = reservation.UserId,
                BookId = reservation.BookId,
                ReservationDate = DateTime.SpecifyKind(reservation.ReservationDate, DateTimeKind.Utc)
            };
            reservation.Status = "Borrowed";
            reservation.CheckoutDate = checkoutDate;
            reservation.DueDate = dueDate;
            _context.ReservationEvents.Add(new ReservationEventOutbox
            {
                Id = acceptedEvent.EventId,
                ReservationId = reservation.Id,
                CreatedAtUtc = acceptedEvent.TimestampUtc,
                Payload = JsonSerializer.Serialize(acceptedEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });

            try
            {
                // Borrow record, status, dates, and event commit together.
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "This reservation was already processed. Refresh the queue." });
            }
            catch (DbUpdateException)
            {
                // A competing acceptance can hit the unique event index before the status check.
                var currentStatus = await _context.Reservations.AsNoTracking()
                    .Where(r => r.Id == id).Select(r => r.Status).SingleOrDefaultAsync(cancellationToken);
                if (currentStatus != "Pending")
                    return Conflict(new { message = "This reservation was already processed. Refresh the queue." });
                throw;
            }

            return Ok(new
            {
                reservation.Id,
                reservation.Status,
                borrowRecordId = borrowRecord.Id,
                checkoutDate,
                dueDate,
                eventId = acceptedEvent.EventId,
                message = "Book borrowed. Reservation acceptance event queued for Kafka."
            });
        }

        [HttpPost("{id:int}/reject")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(int id, CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (reservation == null)
                return NotFound(new { message = "Reservation not found." });
            if (reservation.Status != "Pending")
                return Conflict(new { message = "Only pending reservations can be rejected. Refresh the queue." });

            // Reserving did not decrement inventory, so cancellation only changes Lending state.
            reservation.Status = "Cancelled";
            var cancelledEvent = new ReservationCancelledEvent
            {
                ReservationId = reservation.Id,
                UserId = reservation.UserId,
                BookId = reservation.BookId,
                ReservationDate = DateTime.SpecifyKind(reservation.ReservationDate, DateTimeKind.Utc)
            };
            _context.ReservationEvents.Add(new ReservationEventOutbox
            {
                Id = cancelledEvent.EventId,
                ReservationId = reservation.Id,
                CreatedAtUtc = cancelledEvent.TimestampUtc,
                Payload = JsonSerializer.Serialize(cancelledEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "This reservation was already processed. Refresh the queue." });
            }

            catch (DbUpdateException)
            {
                var currentStatus = await _context.Reservations.AsNoTracking()
                    .Where(r => r.Id == id).Select(r => r.Status).SingleOrDefaultAsync(cancellationToken);
                if (currentStatus != "Pending")
                    return Conflict(new { message = "This reservation was already processed. Refresh the queue." });
                throw;
            }
            return Ok(new { reservation.Id, reservation.Status, eventId = cancelledEvent.EventId,
                message = "Reservation cancelled. Cancellation event queued for Kafka. Copy counts are unchanged." });
        }

        [HttpPost]
        public async Task<ActionResult<ReservationResponse>> ReserveBook([FromBody] ReserveBookRequest request)
        {
            // Extract User ID from JWT
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                              ?? User.FindFirst("sub")?.Value;
                              
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new { message = "User ID missing from token." });
            }

            if (userIdClaim == "admin-id")
            {
                return BadRequest(new { message = "Admins cannot reserve books. Please log in with a regular user account." });
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = $"Invalid user token format. Expected integer, got: {userIdClaim}" });
            }

            // Sync call to Inventory Service
            var inventoryBaseUrl = _configuration["InventoryService:BaseUrl"];
            if (string.IsNullOrEmpty(inventoryBaseUrl))
            {
                return StatusCode(500, new { message = "Inventory Service configuration is missing." });
            }

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(inventoryBaseUrl);

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync($"/api/Books/{request.BookId}");
            }
            catch (HttpRequestException)
            {
                return StatusCode(503, new { message = "Inventory Service is currently unavailable." });
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound(new { message = $"Book with ID {request.BookId} not found in inventory." });
            }

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, new { message = "Failed to communicate with Inventory Service." });
            }

            var bookData = await response.Content.ReadFromJsonAsync<BookInventoryResponse>();
            if (bookData == null)
            {
                return StatusCode(500, new { message = "Invalid response from Inventory Service." });
            }

            if (bookData.AvailableCopies <= 0)
            {
                return BadRequest(new { message = "Book is currently unavailable for reservation." });
            }

            // Create reservation
            var reservation = new Reservation
            {
                BookId = request.BookId,
                UserId = userId,
                Status = "Pending",
                ReservationDate = DateTime.UtcNow
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var result = new ReservationResponse
            {
                Id = reservation.Id,
                BookId = reservation.BookId,
                UserId = reservation.UserId,
                Status = reservation.Status,
                ReservationDate = reservation.ReservationDate
            };

            return CreatedAtAction(nameof(ReserveBook), new { id = result.Id }, result);
        }

        // Helper class to deserialize inventory response
        private class BookInventoryResponse
        {
            public int AvailableCopies { get; set; }
        }
    }
}
