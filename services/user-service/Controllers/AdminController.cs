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

        [HttpPost("users/{id}/accept")]
        public IActionResult AcceptUser(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id && !u.IsValidated);
            if (user == null)
            {
                return NotFound(new { message = "Pending user not found." });
            }

            user.IsValidated = true;
            _context.SaveChanges();

            return Ok(new { message = "User accepted successfully." });
        }

        [HttpPost("users/{id}/reject")]
        public IActionResult RejectUser(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id && !u.IsValidated);
            if (user == null)
            {
                return NotFound(new { message = "Pending user not found." });
            }

            _context.Users.Remove(user);
            _context.SaveChanges();

            return Ok(new { message = "User rejected and deleted successfully." });
        }

        [HttpPost("users/{id}/revoke")]
        public IActionResult RevokeUser(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id && u.IsValidated && u.Role != "Admin");
            if (user == null)
            {
                return NotFound(new { message = "Active user not found." });
            }

            user.IsValidated = false;
            _context.SaveChanges();

            return Ok(new { message = "User access revoked successfully." });
        }
    }
}
