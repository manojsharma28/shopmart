using InventoryService.Models;

namespace InventoryService.Services;

public interface IInventoryService
{
    Task<Product> GetProductAsync(string productId);
    Task<List<Product>> GetAllProductsAsync();
    Task<InventoryCheckResponse> CheckInventoryAsync(InventoryCheckRequest request);
    Task<bool> ReserveStockAsync(string orderId, List<InventoryItem> items);
    Task<bool> ConfirmReservationAsync(string orderId);
    Task<bool> CancelReservationAsync(string orderId);
    Task<Product> UpdateStockAsync(StockUpdateRequest request);
}

public class InventoryServiceImpl : IInventoryService
{
    private readonly List<Product> _products;
    private readonly List<StockReservation> _reservations;
    private readonly ILogger<InventoryServiceImpl> _logger;

    public InventoryServiceImpl(ILogger<InventoryServiceImpl> logger)
    {
        _logger = logger;
        _products = new List<Product>();
        _reservations = new List<StockReservation>();
        InitializeDefaultProducts();
    }

    private void InitializeDefaultProducts()
    {
        _products.AddRange(new List<Product>
        {
            new Product
            {
                Id = "prod001",
                Name = "Laptop",
                Sku = "LAP-001",
                Price = 999.99m,
                QuantityInStock = 50,
                ReservedQuantity = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Id = "prod002",
                Name = "Mouse",
                Sku = "MOU-001",
                Price = 29.99m,
                QuantityInStock = 200,
                ReservedQuantity = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Id = "prod003",
                Name = "Keyboard",
                Sku = "KEY-001",
                Price = 79.99m,
                QuantityInStock = 150,
                ReservedQuantity = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Id = "prod004",
                Name = "Monitor",
                Sku = "MON-001",
                Price = 349.99m,
                QuantityInStock = 30,
                ReservedQuantity = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        });

        _logger.LogInformation("Initialized {ProductCount} default products", _products.Count);
    }

    public async Task<Product> GetProductAsync(string productId)
    {
        var product = _products.FirstOrDefault(p => p.Id == productId);
        if (product == null)
        {
            _logger.LogWarning("Product not found: {ProductId}", productId);
        }
        return await Task.FromResult(product);
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        return await Task.FromResult(_products.ToList());
    }

    public async Task<InventoryCheckResponse> CheckInventoryAsync(InventoryCheckRequest request)
    {
        var response = new InventoryCheckResponse
        {
            Items = new List<ProductAvailability>()
        };

        bool allAvailable = true;

        foreach (var item in request.Items)
        {
            var product = await GetProductAsync(item.ProductId);
            if (product == null)
            {
                allAvailable = false;
                response.Items.Add(new ProductAvailability
                {
                    ProductId = item.ProductId,
                    RequestedQuantity = item.Quantity,
                    AvailableQuantity = 0,
                    IsAvailable = false
                });
                continue;
            }

            bool isAvailable = product.AvailableQuantity >= item.Quantity;
            allAvailable = allAvailable && isAvailable;

            response.Items.Add(new ProductAvailability
            {
                ProductId = item.ProductId,
                RequestedQuantity = item.Quantity,
                AvailableQuantity = product.AvailableQuantity,
                IsAvailable = isAvailable
            });
        }

        response.IsAvailable = allAvailable;
        response.Message = allAvailable ? "All items are in stock" : "Some items are out of stock";

        _logger.LogInformation("Inventory check completed. Available: {IsAvailable}", response.IsAvailable);
        return response;
    }

    public async Task<bool> ReserveStockAsync(string orderId, List<InventoryItem> items)
    {
        _logger.LogInformation("Attempting to reserve stock for order: {OrderId}", orderId);

        // Check if all items are available
        var checkResponse = await CheckInventoryAsync(new InventoryCheckRequest { Items = items });
        if (!checkResponse.IsAvailable)
        {
            _logger.LogWarning("Cannot reserve stock - items not available for order: {OrderId}", orderId);
            return false;
        }

        // Reserve the items
        foreach (var item in items)
        {
            var product = await GetProductAsync(item.ProductId);
            if (product != null)
            {
                product.ReservedQuantity += item.Quantity;
                product.UpdatedAt = DateTime.UtcNow;

                var reservation = new StockReservation
                {
                    Id = Guid.NewGuid().ToString(),
                    OrderId = orderId,
                    ProductId = item.ProductId,
                    QuantityReserved = item.Quantity,
                    Status = StockReservationStatus.Reserved,
                    ReservedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                };

                _reservations.Add(reservation);
                _logger.LogInformation("Reserved {Quantity} units of {ProductId} for order {OrderId}",
                    item.Quantity, item.ProductId, orderId);
            }
        }

        return true;
    }

    public async Task<bool> ConfirmReservationAsync(string orderId)
    {
        var reservations = _reservations.Where(r => r.OrderId == orderId && r.Status == StockReservationStatus.Reserved).ToList();

        if (!reservations.Any())
        {
            _logger.LogWarning("No reservations found to confirm for order: {OrderId}", orderId);
            return false;
        }

        foreach (var reservation in reservations)
        {
            reservation.Status = StockReservationStatus.Confirmed;
            var product = await GetProductAsync(reservation.ProductId);
            if (product != null)
            {
                product.QuantityInStock -= reservation.QuantityReserved;
                product.ReservedQuantity -= reservation.QuantityReserved;
                product.UpdatedAt = DateTime.UtcNow;
            }
        }

        _logger.LogInformation("Confirmed {ReservationCount} reservations for order: {OrderId}", 
            reservations.Count, orderId);
        return true;
    }

    public async Task<bool> CancelReservationAsync(string orderId)
    {
        var reservations = _reservations.Where(r => r.OrderId == orderId && 
            (r.Status == StockReservationStatus.Reserved || r.Status == StockReservationStatus.Confirmed)).ToList();

        if (!reservations.Any())
        {
            _logger.LogWarning("No reservations found to cancel for order: {OrderId}", orderId);
            return false;
        }

        foreach (var reservation in reservations)
        {
            reservation.Status = StockReservationStatus.Cancelled;
            var product = await GetProductAsync(reservation.ProductId);
            if (product != null)
            {
                product.ReservedQuantity -= reservation.QuantityReserved;
                product.UpdatedAt = DateTime.UtcNow;
            }
        }

        _logger.LogInformation("Cancelled {ReservationCount} reservations for order: {OrderId}", 
            reservations.Count, orderId);
        return true;
    }

    public async Task<Product> UpdateStockAsync(StockUpdateRequest request)
    {
        var product = await GetProductAsync(request.ProductId);
        if (product == null)
        {
            _logger.LogWarning("Product not found for stock update: {ProductId}", request.ProductId);
            return null;
        }

        product.QuantityInStock += request.QuantityChange;
        product.UpdatedAt = DateTime.UtcNow;

        _logger.LogInformation("Updated stock for {ProductId}. Change: {Change}. New quantity: {Quantity}. Reason: {Reason}",
            request.ProductId, request.QuantityChange, product.QuantityInStock, request.Reason);

        return product;
    }
}
