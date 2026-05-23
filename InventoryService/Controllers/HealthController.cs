using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Liveness probe for Kubernetes
    /// </summary>
    [HttpGet]
    public ActionResult<object> GetHealth()
    {
        _logger.LogInformation("Health check requested");
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Readiness probe for Kubernetes
    /// </summary>
    [HttpGet("ready")]
    public ActionResult<object> GetReadiness()
    {
        _logger.LogInformation("Readiness check requested");
        return Ok(new { status = "ready", timestamp = DateTime.UtcNow });
    }
}
