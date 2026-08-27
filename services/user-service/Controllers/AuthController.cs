using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserService.Data;
using UserService.DTOs;
using System.Linq;

namespace UserService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(UserDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // 1. Check Hardcoded Admin Credentials
            var adminEmail = _configuration["AdminCredentials:Email"];
            var adminPassword = _configuration["AdminCredentials:Password"];

            if (request.Email == adminEmail && request.Password == adminPassword)
            {
                var token = GenerateJwtToken("admin-id", request.Email, "Admin", true);
                return Ok(new AuthResponse { Token = token, Role = "Admin", IsValidated = true });
            }

            // 2. Check Database for Regular User
            var user = _context.Users.SingleOrDefault(u => u.Email == request.Email);
            
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            var userToken = GenerateJwtToken(user.Id.ToString(), user.Email, user.Role, user.IsValidated);
            return Ok(new AuthResponse { Token = userToken, Role = user.Role, IsValidated = user.IsValidated });
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            // 1. Check if email already exists
            if (_context.Users.Any(u => u.Email == request.Email))
            {
                return BadRequest(new { message = "Email already in use" });
            }

            // 2. Hash Password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // 3. Create User
            var user = new UserService.Models.User
            {
                Email = request.Email,
                PasswordHash = passwordHash,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = "User",
                IsValidated = false // ensure newly registered users default to false
            };

            // 4. Save to DB
            _context.Users.Add(user);
            _context.SaveChanges();

            return Ok(new { message = "Registration successful. Please wait for an admin to validate your account." });
        }

        private string GenerateJwtToken(string id, string email, string role, bool isValidated)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, id),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimTypes.Role, role),
                new Claim("IsValidated", isValidated.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(2),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            
            return tokenHandler.WriteToken(token);
        }
    }
}
