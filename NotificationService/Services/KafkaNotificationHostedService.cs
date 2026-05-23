using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using NotificationService.Models;

namespace NotificationService.Services;

public interface INotificationSender
{
    Task SendNotificationAsync(NotificationMessage notification);
}

public class EmailNotificationSender : INotificationSender
{
    private readonly ILogger<EmailNotificationSender> _logger;

    public EmailNotificationSender(ILogger<EmailNotificationSender> logger)
    {
        _logger = logger;
    }

    public async Task SendNotificationAsync(NotificationMessage notification)
    {
        var subject = notification.IsPaymentSuccess
            ? "Order Confirmation"
            : "Payment Failed - Action Required";

        var body = $"Order ID: {notification.OrderId}\n" +
                   $"Customer ID: {notification.CustomerId}\n" +
                   $"Status: {(notification.IsPaymentSuccess ? "Success" : "Failure")}\n" +
                   $"Message: {notification.Message}\n" +
                   $"Timestamp: {notification.Timestamp:O}";

        _logger.LogInformation("[Notification] Subject: {Subject} | Body: {Body}", subject, body);

        await Task.CompletedTask;
    }
}

public class KafkaNotificationHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<KafkaNotificationHostedService> _logger;
    private readonly INotificationSender _notificationSender;
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly string _topic;

    private readonly INotificationStore _notificationStore;

    public KafkaNotificationHostedService(
        IConfiguration configuration,
        INotificationSender notificationSender,
        INotificationStore notificationStore,
        ILogger<KafkaNotificationHostedService> logger)
    {
        _configuration = configuration;
        _notificationSender = notificationSender;
        _notificationStore = notificationStore;
        _logger = logger;

        var bootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "kafka:9092";
        _topic = _configuration["Kafka:OrderNotificationTopic"] ?? "order-notifications";
        var groupId = _configuration["Kafka:ConsumerGroupId"] ?? "notification-service-group";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            StatisticsIntervalMs = 5000,
            EnablePartitionEof = true
        };

        _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka error: {Reason}", e.Reason))
            .Build();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Kafka notification consumer for topic {Topic}", _topic);
        _consumer.Subscribe(_topic);

        return Task.Run(async () => await ConsumeLoop(stoppingToken), stoppingToken);
    }

    private async Task ConsumeLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(cancellationToken);
                    if (result == null || result.Message == null)
                    {
                        continue;
                    }

                    if (result.IsPartitionEOF)
                    {
                        _logger.LogDebug("Reached end of partition {Partition}", result.TopicPartition);
                        continue;
                    }

                    NotificationMessage? notification;
                    try
                    {
                        notification = JsonSerializer.Deserialize<NotificationMessage>(result.Message.Value);
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Failed to deserialize Kafka notification message");
                        continue;
                    }

                    if (notification == null)
                    {
                        _logger.LogWarning("Received null notification payload");
                        continue;
                    }

                    await _notificationSender.SendNotificationAsync(notification);
                    _notificationStore.Add(notification);
                    _logger.LogInformation(
                        "Processed notification for order {OrderId} customer {CustomerId} success={Success}",
                        notification.OrderId,
                        notification.CustomerId,
                        notification.IsPaymentSuccess);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Kafka consumer cancellation requested");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in Kafka consumer loop");
                }
            }
        }
        finally
        {
            try
            {
                _consumer.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing Kafka consumer");
            }
        }
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}
