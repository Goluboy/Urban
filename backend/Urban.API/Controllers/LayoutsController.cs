using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Urban.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LayoutsController : ControllerBase
    {
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new { status = "Layouts service is running." });
        }
    }
}
