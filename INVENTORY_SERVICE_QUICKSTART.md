# 🚀 InventoryService - Quick Start Guide

## 30-Second Overview

InventoryService manages product inventory and stock reservations. It:
- Maintains product catalog with stock levels
- Reserves stock when orders are placed
- Confirms/cancels reservations based on payment status (via Kafka)
- Provides REST API for inventory operations

**Port**: 5003 (Docker/K8s), 5001 (HTTPS local)

---

## Option 1: Local Development (Fastest)

### Prerequisites
- .NET 8.0 SDK
- Docker & Docker Compose

### Quick Run

```bash
# From project root
docker-compose up -d

# Test service
curl http://localhost:5003/api/inventory/products

# Access Swagger UI
http://localhost:5003/swagger/index.html
```

### Stop Service
```bash
docker-compose down
```

---

## Option 2: Direct .NET Execution

### Prerequisites
- .NET 8.0 SDK
- Kafka running (Docker)

### Quick Run

```bash
# Start just Kafka (from another terminal)
docker run -d --name kafka confluentinc/cp-kafka:7.5.0 \
  -e KAFKA_BROKER_ID=1 \
  -e KAFKA_ZOOKEEPER_CONNECT=zookeeper:2181 \
  -e KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://localhost:9092

# In InventoryService directory
dotnet restore
dotnet run

# Service runs on https://localhost:7003
```

---

## Option 3: Docker Compose with Full Stack

### Quick Run

```bash
# From project root - starts ALL services
docker-compose up -d

# Verify all services
docker-compose ps

# Check specific service
docker-compose logs inventory-service
```

### Services Running
- Zookeeper (port 2181)
- Kafka (port 9092)
- InventoryService (port 5003)
- OrderService (port 5000)
- PaymentService (port 5001)
- NotificationService

---

## Option 4: Kubernetes Deployment

### Prerequisites
- Kubernetes cluster (minikube, k3s, or cloud)
- kubectl configured
- Docker images built

### Quick Deploy

```bash
# Build image
docker build -f InventoryService/Dockerfile -t inventory-service:latest .

# If using minikube
minikube image load inventory-service:latest

# Ensure Kafka is deployed first
kubectl apply -f OrderService/kubernetes/kafka-deployment.yaml

# Deploy InventoryService
kubectl apply -f InventoryService/kubernetes/inventory-service-deployment.yaml

# Verify
kubectl get pods -l app=inventory-service
kubectl logs -l app=inventory-service -f
```

---

## Quick API Tests

### Get All Products
```bash
curl http://localhost:5003/api/inventory/products
```

### Check Stock Availability
```bash
curl -X POST http://localhost:5003/api/inventory/check \
  -H "Content-Type: application/json" \
  -d '{
    "items": [
      {"productId": "prod001", "quantity": 5},
      {"productId": "prod002", "quantity": 10}
    ]
  }'
```

### Reserve Stock
```bash
curl -X POST http://localhost:5003/api/inventory/reserve \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "order-123",
    "items": [{"productId": "prod001", "quantity": 2}]
  }'
```

### Confirm Reservation
```bash
curl -X POST http://localhost:5003/api/inventory/confirm/order-123
```

### Cancel Reservation
```bash
curl -X POST http://localhost:5003/api/inventory/cancel/order-123
```

### Update Stock (Admin)
```bash
curl -X PUT http://localhost:5003/api/inventory/update-stock \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "prod001",
    "quantityChange": 10,
    "reason": "Stock replenishment"
  }'
```

### Health Checks
```bash
curl http://localhost:5003/health         # Liveness
curl http://localhost:5003/health/ready   # Readiness
```

---

## Sample Products

| ID | Name | Price | Stock |
|----|------|-------|-------|
| prod001 | Laptop | $999.99 | 50 |
| prod002 | Mouse | $29.99 | 200 |
| prod003 | Keyboard | $79.99 | 150 |
| prod004 | Monitor | $349.99 | 30 |

---

## Common Commands

### Docker Compose

```bash
# Start services
docker-compose up -d

# Stop services
docker-compose down

# View logs
docker-compose logs -f inventory-service

# Restart service
docker-compose restart inventory-service

# Remove all data
docker-compose down -v
```

### Kubernetes

```bash
# Check deployment
kubectl get deployments inventory-service

# Check pods
kubectl get pods -l app=inventory-service

# View logs
kubectl logs -l app=inventory-service -f

# Port forward
kubectl port-forward svc/inventory-service 5003:5003

# Describe pod
kubectl describe pod <pod-name>

# Delete deployment
kubectl delete deployment inventory-service
```

### .NET

```bash
# Build
dotnet build

# Run
dotnet run

# Test
dotnet test

# Publish
dotnet publish -c Release -o ./publish
```

---

## Port Reference

| Service | Local | Docker | K8s |
|---------|-------|--------|-----|
| InventoryService | 5001/7003* | 5003 | 5003 |
| OrderService | 5000/7000* | 5000 | 5000 |
| Kafka | - | 9092 | 9092 |
| Zookeeper | - | 2181 | 2181 |

*HTTPS/HTTP local development ports

---

## Verification Checklist

- [ ] Service is running (`curl http://localhost:5003/health`)
- [ ] Can list products (`curl http://localhost:5003/api/inventory/products`)
- [ ] Can check inventory
- [ ] Can reserve stock
- [ ] Logs show Kafka consumer started
- [ ] Swagger UI accessible (`http://localhost:5003/swagger`)

---

## Troubleshooting

### Service won't start
```bash
# Check logs for errors
docker-compose logs inventory-service

# Verify Kafka is running
docker-compose ps | grep kafka

# Rebuild image
docker-compose build --no-cache inventory-service
```

### Kafka connection issues
```bash
# Check Kafka is accessible
docker exec -it kafka kafka-broker-api-versions --bootstrap-server localhost:9092

# Check topic exists
docker exec -it kafka kafka-topics --list --bootstrap-server kafka:9092
```

### Port already in use
```bash
# Change port in docker-compose.yml
# Or kill process using port
# Windows: netstat -ano | findstr :5003
# Linux/Mac: lsof -i :5003
```

### Reset everything
```bash
# Remove all containers and volumes
docker-compose down -v

# Rebuild and start fresh
docker-compose build --no-cache
docker-compose up -d
```

---

## Documentation

- **Full Details**: [InventoryService/README.md](./InventoryService/README.md)
- **Deployment**: [InventoryService/kubernetes/DEPLOYMENT_GUIDE.md](./InventoryService/kubernetes/DEPLOYMENT_GUIDE.md)
- **API Testing**: [InventoryService/InventoryService.http](./InventoryService/InventoryService.http)
- **Implementation**: [INVENTORY_SERVICE_IMPLEMENTATION.md](./INVENTORY_SERVICE_IMPLEMENTATION.md)

---

## Next Steps

1. ✅ InventoryService running
2. ⬜ Integrate with OrderService
3. ⬜ Add database persistence
4. ⬜ Add monitoring/metrics
5. ⬜ Deploy to production

---

**Ready to start?** Choose your option above and run the commands! 🚀
