# OrderService - Implementation Guide

## Overview

The OrderService has been refactored to implement a complete microservices architecture with:

1. **Order Processing**: Accepts orders and orchestrates payment processing
2. **Payment Integration**: Communicates with PaymentService via HTTP
3. **Event-Driven Notifications**: Uses Kafka for asynchronous notifications
4. **Kubernetes Deployment**: Full Kubernetes manifests for cloud deployment
5. **Resilience**: Circuit breaker pattern with Polly for fault tolerance
6. **Health Checks**: Liveness and readiness probes for Kubernetes

## Architecture

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ POST /api/orders
       ▼
┌──────────────────┐
│  OrderService    │
│                  │
│  Order           │
│  Processing      │──────HTTP─────► PaymentService
│                  │
│                  │──────Kafka─────► [order-notifications]
└──────────────────┘                      │
                                          ▼
                                 NotificationService
                                 (Kafka Consumer)
                                 Sends Email/SMS/Push
```

## Key Components

### 1. Order Model (Order.cs)

```csharp
public class Order
{
    public string Id { get; set; }              // Unique order ID
    public string CustomerId { get; set; }       // Customer reference
    public decimal Amount { get; set; }          // Total order amount
    public List<OrderItem> Items { get; set; }   // Order items
    public OrderStatus Status { get; set; }      // Current status
    public DateTime CreatedAt { get; set; }      // Creation timestamp
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
```

### 2. Kafka Producer Service

**Location**: `Services/KafkaProducerService.cs`

Publishes order status notifications to Kafka topic `order-notifications`:

```csharp
public interface IKafkaProducerService
{
    Task PublishNotificationAsync(NotificationMessage notification);
}
```

### 3. Orders Controller

**Location**: `Controllers/OrdersController.cs`

- `POST /api/orders` - Create new order
- `GET /api/orders/{orderId}` - Get order details

Order processing flow:
1. Validates order data
2. Sets status to `PaymentProcessing`
3. Calls PaymentService with order details
4. Publishes notification to Kafka (success or failure)
5. Returns appropriate response

### 4. Health Controller

**Location**: `Controllers/HealthController.cs`

- `GET /health` - Liveness probe (Kubernetes)
- `GET /health/ready` - Readiness probe (Kubernetes)

## Configuration

### appsettings.json

```json
{
  "PaymentService": {
    "Url": "http://payment-service:5000/"
  },
  "Kafka": {
    "BootstrapServers": "kafka:9092"
  }
}
```

### Environment Variables (Kubernetes)

```yaml
- name: Kafka__BootstrapServers
  value: "kafka:9092"
- name: PaymentService__Url
  value: "http://payment-service:5000/"
```

## API Endpoints

### Create Order

```bash
POST /api/orders
Content-Type: application/json

{
  "customerId": "cust123",
  "amount": 99.99,
  "items": [
    {
      "productId": "prod001",
      "quantity": 2,
      "price": 49.99
    }
  ]
}
```

**Success Response (200)**:
```json
{
  "message": "Order placed and payment succeeded",
  "order": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "customerId": "cust123",
    "amount": 99.99,
    "status": "PaymentSucceeded",
    "createdAt": "2024-05-18T12:00:00Z"
  }
}
```

**Payment Failed (400)**:
```json
{
  "message": "Payment failed",
  "error": "Insufficient funds"
}
```

**Service Unavailable (503)**:
```json
{
  "error": "Payment service unavailable"
}
```

### Get Order

```bash
GET /api/orders/{orderId}
```

### Health Check

```bash
GET /health
```

Response:
```json
{
  "status": "healthy",
  "timestamp": "2024-05-18T12:00:00Z"
}
```

## Error Handling

The service implements resilient error handling:

1. **HTTP Request Failures**: Retry policy with exponential backoff (3 attempts)
2. **Circuit Breaker**: Opens after 5 failures, waits 30 seconds before retry
3. **Kafka Publishing Failures**: Logged but don't fail the order (fire-and-forget)
4. **Validation Errors**: Return 400 Bad Request

## Running Locally

### Prerequisites

- .NET 8.0 SDK
- Docker (for Kafka)
- Nuget packages installed

### Install Dependencies

```bash
cd OrderService
dotnet restore
```

### Run with Docker Compose

```bash
# From workspace root
docker-compose up -d

# Build and run OrderService
cd OrderService
dotnet run
```

The service will be available at `http://localhost:5000/api/orders`

## Kubernetes Deployment

See `kubernetes/DEPLOYMENT_GUIDE.md` for complete Kubernetes setup instructions.

### Quick Deploy

```bash
# Deploy Kafka
kubectl apply -f kubernetes/kafka-deployment.yaml

# Deploy OrderService
kubectl apply -f kubernetes/order-service-deployment.yaml

# Check status
kubectl get deployments
kubectl get pods
```

## Kafka Integration

### Topic: order-notifications

**Message Format**:
```json
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "cust123",
  "message": "Your order 550e8400-e29b-41d4-a716-446655440000 has been successfully placed and paid.",
  "isPaymentSuccess": true,
  "timestamp": "2024-05-18T12:00:00Z"
}
```

NotificationService subscribes to this topic and sends notifications to customers.

## Testing

### Unit Tests Example

```csharp
[Test]
public async Task CreateOrder_WithValidOrder_CallsPaymentService()
{
    // Arrange
    var order = new Order 
    { 
        CustomerId = "cust123", 
        Amount = 100 
    };
    
    // Act
    var response = await _controller.CreateOrder(order);
    
    // Assert
    Assert.IsNotNull(response);
}
```

### Integration Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter ClassName=OrdersControllerTests
```

## Performance Considerations

1. **Connection Pooling**: HttpClient configured with pooling
2. **Async/Await**: All I/O operations are async
3. **Circuit Breaker**: Prevents cascading failures
4. **Kafka Batching**: Messages batched before publishing
5. **Resource Limits**: Kubernetes resource requests/limits defined

## Security

1. **HTTPS**: Configure in Kubernetes ingress
2. **Service-to-Service**: Internal cluster communication (secure by default)
3. **API Key**: Add if exposing externally
4. **Input Validation**: Validates order data
5. **Secrets**: Use Kubernetes Secrets for sensitive data

## Monitoring

### Logging

- All significant operations logged
- Error tracking with exception details
- Service call tracing

### Metrics

Add Prometheus integration:

```csharp
builder.Services.AddPrometheusMetrics();
```

### Health Checks

Kubernetes automatically monitors:
- Liveness probe: Container health
- Readiness probe: Service ready to accept traffic

## Future Enhancements

1. **Database**: Add persistent storage for order history
2. **Caching**: Redis for order lookups
3. **Event Sourcing**: Complete event history
4. **SAGA Pattern**: Distributed transactions
5. **API Gateway**: Central entry point
6. **Authentication**: JWT/OAuth2
7. **Rate Limiting**: Prevent abuse
8. **Contract Testing**: Ensure API compatibility
