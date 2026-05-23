using Confluent.Kafka;
using System.Text.Json;

namespace OrderService.Services
{
    public interface IKafkaProducerService
    {
        Task PublishNotificationAsync(NotificationMessage notification);
    }

    public class KafkaProducerService : IKafkaProducerService, IDisposable
    {
        private readonly IProducer<Null, string> _producer;
        private readonly ILogger<KafkaProducerService> _logger;
        private const string NotificationTopic = "order-notifications";

        public KafkaProducerService(IConfiguration configuration, ILogger<KafkaProducerService> logger, IProducer<Null, string>? producer = null)
        {
            _logger = logger;
            _producer = producer ?? CreateProducer(configuration);
        }

        private static IProducer<Null, string> CreateProducer(IConfiguration configuration)
        {
            var kafkaConfig = new ProducerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "kafka:9092",
                ClientId = "order-service-producer"
            };

            return new ProducerBuilder<Null, string>(kafkaConfig).Build();
        }

        public async Task PublishNotificationAsync(NotificationMessage notification)
        {
            try
            {
                notification.Timestamp = DateTime.UtcNow;
                var messageJson = JsonSerializer.Serialize(notification);

                var result = await _producer.ProduceAsync(
                    NotificationTopic,
                    new Message<Null, string> { Value = messageJson }
                );

                _logger.LogInformation(
                    "Notification published successfully to Kafka topic '{Topic}' at partition {Partition} offset {Offset}",
                    result.Topic, result.Partition, result.Offset
                );
            }
            catch (ProduceException<Null, string> ex)
            {
                _logger.LogError(ex, "Failed to publish notification to Kafka: {Message}", ex.Message);
                throw;
            }
        }

        public void Dispose()
        {
            _producer?.Dispose();
        }
    }
}
