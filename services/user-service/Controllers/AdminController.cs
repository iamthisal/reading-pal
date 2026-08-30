using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Data;
using UserService.DTOs;
using System.Linq;

namespace UserService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly UserDbContext _context;

        public AdminController(UserDbContext context)
        {
            _context = context;
        }

        [HttpGet("message")]
        public IActionResult GetAdminMessage()
        {
            return Ok(new { message = "Hello Admin! This is a highly classified message from the backend that only administrators can see." });
        }

        [HttpGet("users/pending")]
        public IActionResult GetPendingUsers()
        {
            var pendingUsers = _context.Users
                .Where(u => u.Role != "Admin" && !u.IsValidated)
                .Select(u => new UserSummaryResponse
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    IsValidated = u.IsValidated,
                    CreatedAt = u.CreatedAt
                })
                .ToList();

            return Ok(pendingUsers);
        }

        [HttpGet("users/active")]
        public IActionResult GetActiveUsers()
        {
            var activeUsers = _context.Users
                .Where(u => u.Role != "Admin" && u.IsValidated)
                .Select(u => new UserSummaryResponse
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    IsValidated = u.IsValidated,
                    CreatedAt = u.CreatedAt
                })
                .ToList();

            return Ok(activeUsers);
        }
    }
}
