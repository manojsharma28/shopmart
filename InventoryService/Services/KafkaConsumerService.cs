using System.Text.Json;
using Confluent.Kafka;
using InventoryService.Models;

namespace InventoryService.Services;

public interface IKafkaConsumerService
{
    Task StartConsumingAsync(CancellationToken cancellationToken);
}

public class OrderNotificationMessage
{
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
    public string Message { get; set; }
    public bool IsPaymentSuccess { get; set; }
    public DateTime Timestamp { get; set; }
}

public class KafkaConsumerService : IKafkaConsumerService
{
    private readonly IConfiguration _configuration;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<KafkaConsumerService> _logger;

    public KafkaConsumerService(
        IConfiguration configuration,
        IInventoryService inventoryService,
        ILogger<KafkaConsumerService> logger)
    {
        _configuration = configuration;
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public async Task StartConsumingAsync(CancellationToken cancellationToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        var topic = _configuration["Kafka:OrderNotificationTopic"] ?? "order-notifications";
        var groupId = _configuration["Kafka:ConsumerGroupId"] ?? "inventory-service-group";

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            StatisticsIntervalMs = 5000
        };

        try
        {
            using (var consumer = new ConsumerBuilder<string, string>(config)
                .SetErrorHandler((_, e) =>
                {
                    _logger.LogError("Kafka error: {Error}", e.Reason);
                })
                .Build())
            {
                consumer.Subscribe(new[] { topic });
                _logger.LogInformation("Started consuming from Kafka topic: {Topic}", topic);

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = consumer.Consume(cancellationToken);
                        if (consumeResult != null && consumeResult.Message != null)
                        {
                            await ProcessOrderNotificationAsync(consumeResult.Message.Value);
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Kafka consume error");
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Kafka consumer stopped gracefully");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Kafka consumer service");
            throw;
        }
    }

    private async Task ProcessOrderNotificationAsync(string message)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<OrderNotificationMessage>(message);
            if (notification == null)
            {
                _logger.LogWarning("Failed to deserialize order notification");
                return;
            }

            _logger.LogInformation("Processing order notification for OrderId: {OrderId}, PaymentSuccess: {IsPaymentSuccess}",
                notification.OrderId, notification.IsPaymentSuccess);

            if (notification.IsPaymentSuccess)
            {
                // Confirm the reservation when payment is successful
                await _inventoryService.ConfirmReservationAsync(notification.OrderId);
                _logger.LogInformation("Confirmed inventory reservation for successful order: {OrderId}",
                    notification.OrderId);
            }
            else
            {
                // Cancel the reservation when payment fails
                await _inventoryService.CancelReservationAsync(notification.OrderId);
                _logger.LogInformation("Cancelled inventory reservation for failed order: {OrderId}",
                    notification.OrderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order notification");
        }
    }
}
