using Microsoft.AspNetCore.Mvc;
using InventoryService.Models;
using InventoryService.Services;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(IInventoryService inventoryService, ILogger<InventoryController> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    /// <summary>
    /// Get all products with current stock levels
    /// </summary>
    [HttpGet("products")]
    public async Task<ActionResult<IEnumerable<Product>>> GetAllProducts()
    {
        try
        {
            var products = await _inventoryService.GetAllProductsAsync();
            return Ok(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products");
            return StatusCode(500, new { message = "Error retrieving products" });
        }
    }

    /// <summary>
    /// Get specific product details
    /// </summary>
    [HttpGet("products/{productId}")]
    public async Task<ActionResult<Product>> GetProduct(string productId)
    {
        try
        {
            var product = await _inventoryService.GetProductAsync(productId);
            if (product == null)
            {
                return NotFound(new { message = $"Product {productId} not found" });
            }
            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product {ProductId}", productId);
            return StatusCode(500, new { message = "Error retrieving product" });
        }
    }

    /// <summary>
    /// Check if inventory is available for items
    /// </summary>
    [HttpPost("check")]
    public async Task<ActionResult<InventoryCheckResponse>> CheckInventory([FromBody] InventoryCheckRequest request)
    {
        try
        {
            if (request?.Items == null || !request.Items.Any())
            {
                return BadRequest(new { message = "Items list cannot be empty" });
            }

            var response = await _inventoryService.CheckInventoryAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking inventory");
            return StatusCode(500, new { message = "Error checking inventory" });
        }
    }

    /// <summary>
    /// Reserve stock for an order
    /// </summary>
    [HttpPost("reserve")]
    public async Task<ActionResult<object>> ReserveStock([FromBody] ReserveStockRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request?.OrderId) || request.Items == null || !request.Items.Any())
            {
                return BadRequest(new { message = "OrderId and Items are required" });
            }

            var success = await _inventoryService.ReserveStockAsync(request.OrderId, request.Items);
            
            if (!success)
            {
                return BadRequest(new { message = "Failed to reserve stock - insufficient inventory" });
            }

            return Ok(new { message = "Stock reserved successfully", orderId = request.OrderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reserving stock for order {OrderId}", request?.OrderId);
            return StatusCode(500, new { message = "Error reserving stock" });
        }
    }

    /// <summary>
    /// Confirm a stock reservation (typically after payment succeeds)
    /// </summary>
    [HttpPost("confirm/{orderId}")]
    public async Task<ActionResult<object>> ConfirmReservation(string orderId)
    {
        try
        {
            var success = await _inventoryService.ConfirmReservationAsync(orderId);
            
            if (!success)
            {
                return NotFound(new { message = $"No reservations found for order {orderId}" });
            }

            return Ok(new { message = "Reservation confirmed", orderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming reservation for order {OrderId}", orderId);
            return StatusCode(500, new { message = "Error confirming reservation" });
        }
    }

    /// <summary>
    /// Cancel a stock reservation (typically when payment fails)
    /// </summary>
    [HttpPost("cancel/{orderId}")]
    public async Task<ActionResult<object>> CancelReservation(string orderId)
    {
        try
        {
            var success = await _inventoryService.CancelReservationAsync(orderId);
            
            if (!success)
            {
                return NotFound(new { message = $"No reservations found for order {orderId}" });
            }

            return Ok(new { message = "Reservation cancelled", orderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling reservation for order {OrderId}", orderId);
            return StatusCode(500, new { message = "Error cancelling reservation" });
        }
    }

    /// <summary>
    /// Update stock for a product (admin operation)
    /// </summary>
    [HttpPut("update-stock")]
    public async Task<ActionResult<Product>> UpdateStock([FromBody] StockUpdateRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request?.ProductId))
            {
                return BadRequest(new { message = "ProductId is required" });
            }

            var product = await _inventoryService.UpdateStockAsync(request);
            
            if (product == null)
            {
                return NotFound(new { message = $"Product {request.ProductId} not found" });
            }

            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock for product {ProductId}", request?.ProductId);
            return StatusCode(500, new { message = "Error updating stock" });
        }
    }
}

public class ReserveStockRequest
{
    public string OrderId { get; set; }
    public List<InventoryItem> Items { get; set; }
}
