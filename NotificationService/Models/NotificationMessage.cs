using System;

namespace NotificationService.Models
{
    public class NotificationMessage
    {
        public string? OrderId { get; set; }
        public string? CustomerId { get; set; }
        public string? Message { get; set; }
        public bool IsPaymentSuccess { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
