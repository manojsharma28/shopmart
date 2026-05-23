# Inventory Service - Implementation Guide

## Overview

The InventoryService manages product inventory and stock levels across the microservices ecosystem. It provides APIs to check product availability, reserve stock for orders, and automatically update inventory based on order payment status via Kafka events.

## Architecture

```
┌─────────────────────┐
│  Order Service      │
│  (HTTP Request)     │
└──────────┬──────────┘
           │
           │ GET /api/inventory/products
           │ POST /api/inventory/check
           │ POST /api/inventory/reserve
           │
           ▼
┌──────────────────────────┐
│  Inventory Service       │
│  - Check Stock           │
│  - Reserve Stock         │
│  - Update Inventory      │
└──────────┬───────────────┘
           │
           │ Kafka Consumer
           │ (order-notifications)
           ▼
    ┌────────────────┐
    │ Order Service  │ (Order Success/Failed Event)
    └────────────────┘
```

## Key Components

### 1. Product Model (Models/Product.cs)

```csharp
public class Product
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Sku { get; set; }
    public decimal Price { get; set; }
    public int QuantityInStock { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; } // Read-only: QuantityInStock - ReservedQuantity
}

public enum StockReservationStatus
{
    Reserved,
    Confirmed,
    Cancelled,
    Expired
}
```

### 2. Inventory Service (Services/InventoryService.cs)

Core business logic for inventory management:
- **GetProductAsync()** - Retrieve product details
- **GetAllProductsAsync()** - List all products with current stock
- **CheckInventoryAsync()** - Verify stock availability for items
- **ReserveStockAsync()** - Reserve items for an order (tentative hold)
- **ConfirmReservationAsync()** - Confirm and deduct from stock (on payment success)
- **CancelReservationAsync()** - Release reserved items (on payment failure)
- **UpdateStockAsync()** - Admin operation to adjust stock

### 3. Kafka Consumer Service (Services/KafkaConsumerService.cs)

Listens to `order-notifications` topic and reacts to order payment status:
- On **payment success**: Confirms inventory reservation (locks stock)
- On **payment failure**: Cancels reservation (releases hold)

### 4. Inventory Controller (Controllers/InventoryController.cs)

REST endpoints for inventory operations:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/inventory/products` | GET | Get all products |
| `/api/inventory/products/{id}` | GET | Get product details |
| `/api/inventory/check` | POST | Check stock availability |
| `/api/inventory/reserve` | POST | Reserve stock for order |
| `/api/inventory/confirm/{orderId}` | POST | Confirm reservation |
| `/api/inventory/cancel/{orderId}` | POST | Cancel reservation |
| `/api/inventory/update-stock` | PUT | Update stock (admin) |

### 5. Health Controller (Controllers/HealthController.cs)

Kubernetes health probes:
- `GET /health` - Liveness probe
- `GET /health/ready` - Readiness probe

## API Examples

### 1. Check Inventory Availability

```bash
curl -X POST http://localhost:5003/api/inventory/check \
  -H "Content-Type: application/json" \
  -d '{
    "items": [
      {
        "productId": "prod001",
        "quantity": 2
      },
      {
        "productId": "prod002",
        "quantity": 5
      }
    ]
  }'
```

Response:
```json
{
  "isAvailable": true,
  "message": "All items are in stock",
  "items": [
    {
      "productId": "prod001",
      "requestedQuantity": 2,
      "availableQuantity": 50,
      "isAvailable": true
    },
    {
      "productId": "prod002",
      "requestedQuantity": 5,
      "availableQuantity": 200,
      "isAvailable": true
    }
  ]
}
```

### 2. Reserve Stock for Order

```bash
curl -X POST http://localhost:5003/api/inventory/reserve \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "order-12345",
    "items": [
      {
        "productId": "prod001",
        "quantity": 2
      }
    ]
  }'
```

Response:
```json
{
  "message": "Stock reserved successfully",
  "orderId": "order-12345"
}
```

### 3. Get All Products

```bash
curl http://localhost:5003/api/inventory/products
```

Response:
```json
[
  {
    "id": "prod001",
    "name": "Laptop",
    "sku": "LAP-001",
    "price": 999.99,
    "quantityInStock": 50,
    "reservedQuantity": 2,
    "availableQuantity": 48,
    "createdAt": "2024-05-18T00:00:00Z",
    "updatedAt": "2024-05-18T12:30:45Z"
  }
]
```

## Stock Reservation Flow

### Order Placed → Stock Reserved

1. **Order Service** calls `POST /api/inventory/reserve` with order items
2. **Inventory Service** checks availability and creates temporary hold
3. Stock is marked as "Reserved" but NOT deducted from inventory

### Payment Succeeds → Reservation Confirmed

1. **Order Service** publishes success event to Kafka `order-notifications` topic
2. **Inventory Service** consumes event and calls `ConfirmReservationAsync()`
3. Reserved quantity is converted to actual stock deduction
4. Product's QuantityInStock decreases

### Payment Fails → Reservation Cancelled

1. **Order Service** publishes failure event to Kafka `order-notifications` topic
2. **Inventory Service** consumes event and calls `CancelReservationAsync()`
3. Reserved hold is released
4. Stock returns to available

## Configuration

### appsettings.json

```json
{
  "Kafka": {
    "BootstrapServers": "kafka:9092",
    "OrderNotificationTopic": "order-notifications",
    "ConsumerGroupId": "inventory-service-group"
  }
}
```

### Environment Variables (for Kubernetes)

```yaml
env:
- name: Kafka__BootstrapServers
  value: "kafka:9092"
- name: Kafka__OrderNotificationTopic
  value: "order-notifications"
- name: Kafka__ConsumerGroupId
  value: "inventory-service-group"
```

## Running Locally

### Prerequisites
- .NET 8.0 SDK
- Docker & Docker Compose (for Kafka)

### Development

```bash
# Install dependencies
dotnet restore

# Run locally (requires Kafka from docker-compose)
dotnet run

# Access Swagger UI
# http://localhost:5003/swagger/index.html
```

### With Docker Compose

From root directory:

```bash
# Start all services
docker-compose up -d

# Check Inventory Service
curl http://localhost:5003/health

# View logs
docker-compose logs -f inventory-service
```

## Kubernetes Deployment

### Build Docker Image

```bash
docker build -f InventoryService/Dockerfile -t inventory-service:latest .
```

### Deploy to Kubernetes

```bash
# Apply deployment
kubectl apply -f InventoryService/kubernetes/inventory-service-deployment.yaml

# Verify deployment
kubectl get deployments
kubectl get pods -l app=inventory-service

# Check logs
kubectl logs -l app=inventory-service -f

# Port forward for local testing
kubectl port-forward service/inventory-service 5003:5003
```

### Scaling

```bash
# Scale to 3 replicas
kubectl scale deployment inventory-service --replicas=3

# Auto-scaling (requires metrics server)
kubectl autoscale deployment inventory-service --min=2 --max=5 --cpu-percent=80
```

## Default Products

The service initializes with sample products:

| Product | SKU | Price | Initial Stock |
|---------|-----|-------|---------------|
| Laptop | LAP-001 | $999.99 | 50 |
| Mouse | MOU-001 | $29.99 | 200 |
| Keyboard | KEY-001 | $79.99 | 150 |
| Monitor | MON-001 | $349.99 | 30 |

## Testing

### Unit Tests (Pseudo-code)

```csharp
[TestClass]
public class InventoryServiceTests
{
    [TestMethod]
    public async Task ReserveStock_WithAvailableItems_ReturnsTrue()
    {
        // Arrange
        var service = new InventoryServiceImpl(logger);
        var items = new List<InventoryItem> { new { ProductId = "prod001", Quantity = 5 } };
        
        // Act
        var result = await service.ReserveStockAsync("order-1", items);
        
        // Assert
        Assert.IsTrue(result);
    }
}
```

### Integration Tests

```bash
# Test stock check
curl http://localhost:5003/api/inventory/check -X POST \
  -H "Content-Type: application/json" \
  -d '{"items":[{"productId":"prod001","quantity":10}]}'

# Test reservation
curl http://localhost:5003/api/inventory/reserve -X POST \
  -H "Content-Type: application/json" \
  -d '{"orderId":"test-order","items":[{"productId":"prod001","quantity":5}]}'

# Confirm reservation
curl http://localhost:5003/api/inventory/confirm/test-order -X POST

# Check stock levels
curl http://localhost:5003/api/inventory/products
```

## Monitoring & Observability

### Health Checks

```bash
# Liveness
curl http://localhost:5003/health

# Readiness
curl http://localhost:5003/health/ready
```

### Logging

Structured logging with correlation to:
- Order operations
- Kafka events
- Inventory updates
- Stock reservations

View logs:
```bash
# Docker Compose
docker-compose logs -f inventory-service

# Kubernetes
kubectl logs -l app=inventory-service -f
```

## Production Considerations

1. **Persistence**: Current implementation uses in-memory storage. For production, use:
   - SQL Database (SQL Server, PostgreSQL)
   - NoSQL Database (MongoDB, DynamoDB)
   - Redis for cache layer

2. **Concurrency**: Add distributed locking for:
   - Stock reservation under high concurrency
   - Consider Redlock or database-level locks

3. **Expiration**: Stock reservations expire after 1 hour
   - Add background job to clean up expired reservations

4. **Audit Trail**: Log all inventory modifications for compliance

5. **Circuit Breaker**: Add resilience for Kafka connection failures

6. **Metrics**: Integrate Prometheus/Grafana for:
   - Stock levels by product
   - Reservation success rate
   - API response times

## Dependencies

- **Confluent.Kafka** (2.3.0) - Kafka consumer integration
- **Swashbuckle.AspNetCore** (6.6.2) - Swagger/OpenAPI documentation
- **.NET 8.0** - Runtime framework

## License

Part of Mart Microservices Architecture
