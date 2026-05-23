using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OrderService.Services;
using Xunit;

namespace OrderService.Tests
{
    public class KafkaProducerServiceTests
    {
        private static IConfiguration CreateTestConfiguration()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Kafka:BootstrapServers"] = "localhost:9092"
                })
                .Build();
        }

        [Fact]
        public async Task PublishNotificationAsync_SetsTimestampAndPublishesMessage()
        {
            // Arrange
            var mockProducer = new Mock<IProducer<Null, string>>();
            var messageCaptured = new Message<Null, string>();
            mockProducer
                .Setup(p => p.ProduceAsync(
                    It.Is<string>(topic => topic == "order-notifications"),
                    It.IsAny<Message<Null, string>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, Message<Null, string>, CancellationToken>((topic, message, token) =>
                {
                    messageCaptured = message;
                })
                .ReturnsAsync(new DeliveryResult<Null, string>
                {
                    Topic = "order-notifications",
                    Partition = new Partition(0),
                    Offset = new Offset(1),
                    Message = new Message<Null, string> { Value = "{\"ok\":true}" }
                });

            var logger = Mock.Of<ILogger<KafkaProducerService>>();
            var sut = new KafkaProducerService(CreateTestConfiguration(), logger, mockProducer.Object);
            var notification = new NotificationMessage
            {
                OrderId = "order-123",
                CustomerId = "customer-abc",
                Message = "Payment completed",
                IsPaymentSuccess = true
            };

            // Act
            await sut.PublishNotificationAsync(notification);

            // Assert
            Assert.NotEqual(default, notification.Timestamp);
            mockProducer.Verify(p => p.ProduceAsync(
                "order-notifications",
                It.IsAny<Message<Null, string>>(),
                It.IsAny<CancellationToken>()), Times.Once);

            Assert.NotNull(messageCaptured.Value);
            var payload = JsonSerializer.Deserialize<NotificationMessage>(messageCaptured.Value);
            Assert.NotNull(payload);
            Assert.Equal("order-123", payload.OrderId);
            Assert.Equal("customer-abc", payload.CustomerId);
            Assert.Equal("Payment completed", payload.Message);
            Assert.True(payload.IsPaymentSuccess);
            Assert.NotEqual(default, payload.Timestamp);
        }

        [Fact]
        public async Task PublishNotificationAsync_ThrowsWhenProducerFails()
        {
            // Arrange
            var mockProducer = new Mock<IProducer<Null, string>>();
            var producerException = new ProduceException<Null, string>(new Error(ErrorCode.Local_MsgTimedOut), new DeliveryResult<Null, string>());
            mockProducer
                .Setup(p => p.ProduceAsync(
                    It.IsAny<string>(),
                    It.IsAny<Message<Null, string>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(producerException);

            var logger = Mock.Of<ILogger<KafkaProducerService>>();
            var sut = new KafkaProducerService(CreateTestConfiguration(), logger, mockProducer.Object);
            var notification = new NotificationMessage
            {
                OrderId = "order-456",
                CustomerId = "customer-def",
                Message = "Payment failed",
                IsPaymentSuccess = false
            };

            // Act / Assert
            var exception = await Assert.ThrowsAsync<ProduceException<Null, string>>(() => sut.PublishNotificationAsync(notification));
            Assert.Equal(ErrorCode.Local_MsgTimedOut, exception.Error.Code);
            mockProducer.Verify(p => p.ProduceAsync(
                "order-notifications",
                It.IsAny<Message<Null, string>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
