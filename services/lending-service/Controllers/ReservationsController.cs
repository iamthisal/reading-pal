using System.Security.Claims;
using LendingService.Data;
using LendingService.DTOs;
using LendingService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
