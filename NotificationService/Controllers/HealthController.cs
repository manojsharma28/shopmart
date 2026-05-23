using Microsoft.AspNetCore.Mvc;

namespace NotificationService.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;

        public HealthController(ILogger<HealthController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Health()
        {
            _logger.LogInformation("Health check requested");
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }

        [HttpGet("ready")]
        public IActionResult Ready()
        {
            _logger.LogInformation("Readiness check requested");
            return Ok(new { status = "ready", timestamp = DateTime.UtcNow });
        }
    }
}
