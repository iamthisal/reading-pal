using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UserService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        [HttpGet("message")]
        public IActionResult GetAdminMessage()
        {
            return Ok(new { message = "Hello Admin! This is a highly classified message from the backend that only administrators can see." });
        }
    }
}
