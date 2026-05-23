using Microsoft.AspNetCore.Mvc;
using NotificationService.Models;
using NotificationService.Services;

namespace NotificationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationStore _notificationStore;

    public NotificationsController(INotificationStore notificationStore)
    {
        _notificationStore = notificationStore;
    }

    [HttpGet]
    public ActionResult<IEnumerable<NotificationMessage>> GetNotifications([FromQuery] int count = 20)
    {
        return Ok(_notificationStore.GetLatest(count));
    }
}
