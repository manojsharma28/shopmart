using NotificationService.Models;

namespace NotificationService.Services;

public interface INotificationStore
{
    void Add(NotificationMessage notification);
    IReadOnlyList<NotificationMessage> GetLatest(int count = 20);
}

public class NotificationStore : INotificationStore
{
    private readonly List<NotificationMessage> _notifications = new();
    private readonly object _lock = new();

    public void Add(NotificationMessage notification)
    {
        if (notification == null)
        {
            return;
        }

        lock (_lock)
        {
            _notifications.Add(notification);
            if (_notifications.Count > 100)
            {
                _notifications.RemoveAt(0);
            }
        }
    }

    public IReadOnlyList<NotificationMessage> GetLatest(int count = 20)
    {
        lock (_lock)
        {
            return _notifications.TakeLast(count).Reverse().ToArray();
        }
    }
}
