using System;

public class Order
{
    public string? Id { get; set; }
    public string? CustomerId { get; set; }
    public decimal Amount { get; set; }
    public List<OrderItem>? Items { get; set; }
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }

    public Order()
    {
        Id = Guid.NewGuid().ToString();
        CreatedAt = DateTime.UtcNow;
        Status = OrderStatus.Pending;
        Items = new List<OrderItem>();
    }
}

public class OrderItem
{
    public string? ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public enum OrderStatus
{
    Pending,
    PaymentProcessing,
    PaymentSucceeded,
    PaymentFailed,
    Completed,
    Cancelled
}

public class PaymentRequest
{
    public string? OrderId { get; set; }
    public decimal Amount { get; set; }
    public string? CustomerId { get; set; }
}

public class PaymentResponse
{
    public string? OrderId { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public class NotificationMessage
{
    public string? OrderId { get; set; }
    public string? CustomerId { get; set; }
    public string? Message { get; set; }
    public bool IsPaymentSuccess { get; set; }
    public DateTime Timestamp { get; set; }
}