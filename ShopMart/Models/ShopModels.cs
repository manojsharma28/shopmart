namespace ShopMart.Models;

public class Product
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    // InventoryService exposes these fields: QuantityInStock, ReservedQuantity and AvailableQuantity
    public int QuantityInStock { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity => QuantityInStock - ReservedQuantity;
}

public class CartItem
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class OrderCreateRequest
{
    public string? Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public List<CartItem> Items { get; set; } = new();
}

public class OrderRequest
{
    public string? Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public List<CartItem> Items { get; set; } = new();
}

public class NotificationMessage
{
    public string? OrderId { get; set; }
    public string? CustomerId { get; set; }
    public string? Message { get; set; }
    public bool IsPaymentSuccess { get; set; }
    public DateTime Timestamp { get; set; }
}
