using Microsoft.AspNetCore.Mvc;
using MedicalApp.API.Helpers;

namespace MedicalApp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InfoController : ControllerBase
    {
        private const string BackendVersion = "1.0.0";

        [HttpGet("version")]
        public IActionResult GetVersion()
        {
            return Ok(ApiResponse<object>.Success(new { version = BackendVersion }));
        }
    }
}
