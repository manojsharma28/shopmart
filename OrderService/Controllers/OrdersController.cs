using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using OrderService.Services;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKafkaProducerService _kafkaProducer;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IHttpClientFactory httpClientFactory,
            IKafkaProducerService kafkaProducer,
            ILogger<OrdersController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _kafkaProducer = kafkaProducer;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] Order order)
        {
            if (order == null || string.IsNullOrWhiteSpace(order.Id) || string.IsNullOrWhiteSpace(order.CustomerId))
            {
                _logger.LogWarning("Invalid order received.");
                return BadRequest(new { error = "Invalid order data" });
            }

            order.Status = OrderStatus.PaymentProcessing;

            try
            {
                // Call Payment Service
                var paymentRequest = new PaymentRequest
                {
                    OrderId = order.Id,
                    Amount = order.Amount,
                    CustomerId = order.CustomerId
                };

                var client = _httpClientFactory.CreateClient("PaymentService");
                var paymentResponse = await client.PostAsJsonAsync("pay", paymentRequest);

                PaymentResponse? payment = null;
                if (paymentResponse.IsSuccessStatusCode)
                {
                    payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentResponse>();
                }

                // Publish notification based on payment result
                await PublishNotificationAsync(order, payment?.Success ?? false);

                if (payment?.Success == true)
                {
                    order.Status = OrderStatus.PaymentSucceeded;
                    _logger.LogInformation("Order {OrderId} payment succeeded.", order.Id);
                    return Ok(new { message = "Order placed and payment succeeded", order });
                }
                else
                {
                    order.Status = OrderStatus.PaymentFailed;
                    _logger.LogWarning("Order {OrderId} payment failed.", order.Id);
                    return BadRequest(new { message = "Payment failed", error = payment?.Message });
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Payment service error for order {OrderId}", order.Id);
                order.Status = OrderStatus.PaymentFailed;
                
                // Still notify about failure
                await PublishNotificationAsync(order, false);
                
                return StatusCode(503, new { error = "Payment service unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing order {OrderId}", order.Id);
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }

        [HttpGet("{orderId}")]
        public IActionResult GetOrder(string orderId)
        {
            // This would typically fetch from a database
            _logger.LogInformation("Fetching order {OrderId}", orderId);
            return Ok(new { orderId, message = "Order retrieved" });
        }

        private async Task PublishNotificationAsync(Order order, bool paymentSuccess)
        {
            try
            {
                var notification = new NotificationMessage
                {
                    OrderId = order.Id,
                    CustomerId = order.CustomerId,
                    IsPaymentSuccess = paymentSuccess,
                    Message = paymentSuccess
                        ? $"Your order {order.Id} has been successfully placed and paid."
                        : $"Your order {order.Id} payment has failed. Please retry."
                };

                await _kafkaProducer.PublishNotificationAsync(notification);
                _logger.LogInformation("Notification sent for order {OrderId}", order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification for order {OrderId}", order.Id);
                // Don't throw - notification failure shouldn't fail the order
            }
        }
    }
}
