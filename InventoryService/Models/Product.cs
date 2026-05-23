namespace InventoryService.Models;

public class Product
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Sku { get; set; }
    public decimal Price { get; set; }
    public int QuantityInStock { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity => QuantityInStock - ReservedQuantity;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class StockReservation
{
    public string Id { get; set; }
    public string OrderId { get; set; }
    public string ProductId { get; set; }
    public int QuantityReserved { get; set; }
    public StockReservationStatus Status { get; set; }
    public DateTime ReservedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public enum StockReservationStatus
{
    Reserved,
    Confirmed,
    Cancelled,
    Expired
}

public class InventoryCheckRequest
{
    public List<InventoryItem> Items { get; set; }
}

public class InventoryItem
{
    public string ProductId { get; set; }
    public int Quantity { get; set; }
}

public class InventoryCheckResponse
{
    public bool IsAvailable { get; set; }
    public List<ProductAvailability> Items { get; set; }
    public string Message { get; set; }
}

public class ProductAvailability
{
    public string ProductId { get; set; }
    public int RequestedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; }
}

public class StockUpdateRequest
{
    public string ProductId { get; set; }
    public int QuantityChange { get; set; }
    public string Reason { get; set; }
}
