using Microsoft.AspNetCore.Mvc;
using BelekCommunity.Api.Models;

namespace BelekCommunity.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("Users GET endpoint works");
        }

        [HttpPost]
        public IActionResult Create([FromBody] CreateUserRequest request)
        {
            return Ok(new
            {
                Message = "User create request received",
                Email = request.Email
            });
        }

    }
}
