using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UserService.Data;
using UserService.DTOs;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;

namespace UserService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly UserDbContext _context;

        public UserController(UserDbContext context)
        {
            _context = context;
        }

        [HttpGet("profile")]
        public IActionResult GetProfile()
        {
            var userIdStr = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (!int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new { message = "Invalid user token" });
            }

            var user = _context.Users.SingleOrDefault(u => u.Id == userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var response = new UserProfileResponse
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email
            };

            return Ok(response);
        }
    }
}
