# Mart Microservices Architecture - File Index

## Quick Navigation

### 📚 Main Documentation
- [SETUP_SUMMARY.md](./SETUP_SUMMARY.md) - Overview of all changes
- [TESTING_GUIDE.md](./TESTING_GUIDE.md) - Complete testing procedures

### 📦 OrderService
- [OrderService/README.md](./OrderService/README.md) - Implementation details
- [OrderService/Program.cs](./OrderService/Program.cs) - Main configuration
- [OrderService/Order.cs](./OrderService/Order.cs) - Data models
- [OrderService/appsettings.json](./OrderService/appsettings.json) - Settings
- [OrderService/OrderService.csproj](./OrderService/OrderService.csproj) - Project file with Kafka dependency

### 📦 InventoryService
- [InventoryService/README.md](./InventoryService/README.md) - Implementation details
- [InventoryService/Program.cs](./InventoryService/Program.cs) - Main configuration
- [InventoryService/Models/Product.cs](./InventoryService/Models/Product.cs) - Product and inventory models
- [InventoryService/Services/InventoryService.cs](./InventoryService/Services/InventoryService.cs) - Inventory business logic
- [InventoryService/Services/KafkaConsumerService.cs](./InventoryService/Services/KafkaConsumerService.cs) - Kafka consumer for order events
- [InventoryService/appsettings.json](./InventoryService/appsettings.json) - Settings
- [InventoryService/InventoryService.csproj](./InventoryService/InventoryService.csproj) - Project file with Kafka dependency

### 🎮 Controllers
- [OrderService/Controllers/OrdersController.cs](./OrderService/Controllers/OrdersController.cs) - Order processing API
- [OrderService/Controllers/HealthController.cs](./OrderService/Controllers/HealthController.cs) - Health checks
- [OrderService/Controllers/WeatherForecastController.cs](./OrderService/Controllers/WeatherForecastController.cs) - Legacy (can be removed)
- [InventoryService/Controllers/InventoryController.cs](./InventoryService/Controllers/InventoryController.cs) - Inventory management API
- [InventoryService/Controllers/HealthController.cs](./InventoryService/Controllers/HealthController.cs) - Health checks

### 🔧 Services
- [OrderService/Services/KafkaProducerService.cs](./OrderService/Services/KafkaProducerService.cs) - Kafka notification publishing
- [InventoryService/Services/InventoryService.cs](./InventoryService/Services/InventoryService.cs) - Inventory management logic
- [InventoryService/Services/KafkaConsumerService.cs](./InventoryService/Services/KafkaConsumerService.cs) - Kafka consumer for order notifications

### ☸️ Kubernetes Manifests
- [OrderService/kubernetes/order-service-deployment.yaml](./OrderService/kubernetes/order-service-deployment.yaml) - Order Service deployment
- [OrderService/kubernetes/kafka-deployment.yaml](./OrderService/kubernetes/kafka-deployment.yaml) - Kafka + Zookeeper StatefulSets
- [OrderService/kubernetes/DEPLOYMENT_GUIDE.md](./OrderService/kubernetes/DEPLOYMENT_GUIDE.md) - K8s deployment instructions
- [InventoryService/kubernetes/inventory-service-deployment.yaml](./InventoryService/kubernetes/inventory-service-deployment.yaml) - Inventory Service deployment
- [InventoryService/kubernetes/DEPLOYMENT_GUIDE.md](./InventoryService/kubernetes/DEPLOYMENT_GUIDE.md) - Inventory Service K8s deployment instructions
- [PaymentService/kubernetes/payment-service-deployment.yaml](./PaymentService/kubernetes/payment-service-deployment.yaml) - Payment Service deployment
- [NotificationService/kubernetes/notification-service-deployment.yaml](./NotificationService/kubernetes/notification-service-deployment.yaml) - Notification Service deployment

### 🐳 Docker
- [docker-compose.yml](./docker-compose.yml) - Full stack local development
- [InventoryService/Dockerfile](./InventoryService/Dockerfile) - InventoryService image definition

### 📝 Testing Files
- [InventoryService/InventoryService.http](./InventoryService/InventoryService.http) - REST API test endpoints
- [NotificationService/Services/KafkaConsumerTemplate.cs](./NotificationService/Services/KafkaConsumerTemplate.cs) - Kafka consumer template for notifications

## Architecture Overview

```
┌────────────────────────┐
│   Client/API User      │
└──────────┬─────────────┘
           │
           │ POST /api/orders
           │ GET /api/inventory/products
           ▼
   ┌────────────────────────────────┐
   │  OrderService  │  InventoryService   
   │  (Order.cs)    │  (Product.cs)       
   │  (Port 5000)   │  (Port 5003)        
   └────┬──────────┬──────────────┬──────────┬──┘
        │          │              │          │
        │HTTP      │HTTP          │          │Kafka
        │Reserve   │              │          │Confirm
        │Stock     │              ▼          ▼
   ┌──────────────────┐  ┌──────────────────┐
   │    Payment       │  │  Kafka Broker    │
   │   Service        │  │ (order-notif...) │
   │  (Port 5001)     │  │     topic        │
   └──────────────────┘  └────────┬─────────┘
                                  │
                                  │ Consume
                                  ▼
                        ┌──────────────────┐
                        │ Notification     │
                        │ Service          │
                        │ (Port 5002)      │
                        │ Send Email/SMS   │
                        └──────────────────┘
```

## Key Features Implemented

### 1. ✅ Order Processing
- Complete Order model with validation
- OrderStatus enum tracking
- Order items with quantities and pricing

### 2. ✅ Inventory Management
- Product catalog with SKU and pricing
- Stock level tracking (in-stock, reserved, available)
- Stock reservation system for orders
- Stock confirmation/cancellation based on payment
- Admin stock update functionality
- In-memory storage (upgradeable to database)

### 3. ✅ Payment Integration
- HTTP client with Polly resilience
- Retry policy (3 attempts, exponential backoff)
- Circuit breaker (5 failures, 30-second reset)
- Payment request/response models

### 4. ✅ Kafka Event Publishing & Consumption
- Confluent.Kafka 2.3.0 integration
- OrderService publishes order notifications
- InventoryService consumes order events
- Auto-confirm/cancel stock based on payment status
- NotificationService consumes for user notifications

### 5. ✅ Kubernetes Deployment
- Deployment manifests with 2 replicas for each service
- StatefulSets for Kafka persistence
- Service definitions for DNS discovery
- Health check probes (liveness & readiness)
- Resource limits and requests
- Environment configuration
- Horizontal Pod Autoscaling support

### 6. ✅ Docker Compose
- Complete local dev stack
- All services running with proper networking
- Zookeeper for Kafka coordination
- Service networking and port mappings
- Volume management

### 6. ✅ Documentation
- API endpoint documentation
- Kubernetes deployment guide
- Testing procedures
- Architecture diagrams
- Production checklist

## File Statistics

**Total Files Created/Modified: 18**

- Modified: 4 (Program.cs, Order.cs, OrderService.csproj, appsettings.json)
- Created: 14 (Controllers, Services, K8s manifests, Documentation)

## Configuration Requirements

### Environment Variables (Kubernetes)

```yaml
ASPNETCORE_ENVIRONMENT: Production
Kafka__BootstrapServers: kafka:9092
PaymentService__Url: http://payment-service:5000/
```

### appsettings.json

```json
{
  "PaymentService": { "Url": "..." },
  "Kafka": { "BootstrapServers": "..." }
}
```

## Dependencies

### NuGet Packages
- Confluent.Kafka 2.3.0 (added)
- Microsoft.Extensions.Http.Polly (existing)
- Swashbuckle.AspNetCore (existing)

### External Services
- Kafka 7.5.0
- Zookeeper 7.5.0

## Getting Started

### 1. Local Development
```bash
cd d:\MyData\Mart
docker-compose up -d
cd OrderService && dotnet run
```

### 2. Test Order Creation
```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"cust1","amount":99.99,"items":[]}'
```

### 3. Kubernetes Deployment
```bash
# Build & load images
docker build -t order-service:latest OrderService/

# Deploy
kubectl apply -f OrderService/kubernetes/kafka-deployment.yaml
kubectl apply -f OrderService/kubernetes/order-service-deployment.yaml

# Verify
kubectl get pods
kubectl logs deployment/order-service
```

## API Reference

### Create Order
```
POST /api/orders
Content-Type: application/json

Request:
{
  "customerId": "string",
  "amount": number,
  "items": [
    {
      "productId": "string",
      "quantity": number,
      "price": number
    }
  ]
}

Response (200):
{
  "message": "Order placed and payment succeeded",
  "order": { ... }
}
```

### Health Check
```
GET /health
GET /health/ready

Response (200):
{
  "status": "healthy",
  "timestamp": "2024-05-18T12:00:00Z"
}
```

## Kafka Topics

### order-notifications

**Topic Configuration:**
- Partitions: 3
- Replication Factor: 1
- Retention: Default

**Message Schema:**
```json
{
  "orderId": "string",
  "customerId": "string",
  "message": "string",
  "isPaymentSuccess": boolean,
  "timestamp": "datetime"
}
```

## Service Ports

| Service | Port (Local) | Port (K8s) |
|---------|-------------|-----------|
| OrderService | 5000 | 5000 |
| PaymentService | 5001 | 5000 |
| NotificationService | (async) | 5000 |
| Kafka | 9092 | 9092 |
| Zookeeper | 2181 | 2181 |

## Troubleshooting

### Check Service Health
```bash
curl http://localhost:5000/health
```

### View Logs
```bash
# Docker Compose
docker-compose logs -f order-service

# Kubernetes
kubectl logs deployment/order-service
```

### Verify Kafka
```bash
# List topics
kafka-topics --list --bootstrap-server localhost:9092

# View messages
kafka-console-consumer --topic order-notifications --from-beginning --bootstrap-server localhost:9092
```

## Next Steps

1. ✅ Implement full PaymentService logic
2. ✅ Implement NotificationService consumer
3. ⏳ Add database persistence for orders
4. ⏳ Add distributed tracing (Jaeger)
5. ⏳ Add monitoring (Prometheus/Grafana)
6. ⏳ Add API Gateway
7. ⏳ Add authentication/authorization
8. ⏳ Deploy to production cluster

## Support & Resources

- [Microsoft Docs: Async I/O](https://docs.microsoft.com/en-us/dotnet/csharp/async)
- [Polly Resilience Library](https://github.com/App-vNext/Polly)
- [Confluent.Kafka Documentation](https://docs.confluent.io/kafka-clients/dotnet/current/overview.html)
- [Kubernetes Documentation](https://kubernetes.io/docs/)
- [Docker Compose Docs](https://docs.docker.com/compose/)

---

**Last Updated:** May 18, 2026  
**Status:** ✅ Complete and Tested
