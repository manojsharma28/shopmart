# Testing Guide - Microservices Architecture

## Overview

This guide covers testing the OrderService integration with PaymentService and notification pipeline through Kafka.

## Prerequisites

- .NET 8.0 SDK
- Docker Desktop with Kubernetes enabled (or Minikube)
- Postman or curl
- kafka-console-consumer (optional, for Kafka testing)

## Test Scenarios

### 1. Local Testing with Docker Compose

#### Setup

```bash
cd d:\MyData\Mart

# Start all services
docker-compose up -d

# Verify services are running
docker-compose ps

# Check logs
docker-compose logs -f order-service
```

#### Test Cases

**Test 1.1: Valid Order - Success**

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust001",
    "amount": 150.00,
    "items": [
      {
        "productId": "prod001",
        "quantity": 2,
        "price": 75.00
      }
    ]
  }'
```

Expected Response (200):
```json
{
  "message": "Order placed and payment succeeded",
  "order": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "customerId": "cust001",
    "amount": 150.00,
    "status": "PaymentSucceeded",
    "items": [...],
    "createdAt": "2024-05-18T12:00:00Z"
  }
}
```

**Test 1.2: Invalid Order - Missing Customer ID**

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "",
    "amount": 100.00,
    "items": []
  }'
```

Expected Response (400):
```json
{
  "error": "Invalid order data"
}
```

**Test 1.3: Order with Invalid Amount**

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust002",
    "amount": -50.00,
    "items": []
  }'
```

Expected Response: 400 (should validate amount > 0)

**Test 1.4: Health Check**

```bash
curl http://localhost:5000/health
```

Expected Response (200):
```json
{
  "status": "healthy",
  "timestamp": "2024-05-18T12:00:00Z"
}
```

### 2. Kafka Message Verification

#### Check Kafka Topic

```bash
# Access Kafka container
docker exec -it mart-kafka-1 /bin/bash

# List topics
kafka-topics --list --bootstrap-server localhost:9092

# View messages from topic
kafka-console-consumer \
  --topic order-notifications \
  --from-beginning \
  --bootstrap-server localhost:9092 \
  --property print.key=true \
  --property print.value=true
```

#### Expected Kafka Message

```json
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "cust001",
  "message": "Your order 550e8400-e29b-41d4-a716-446655440000 has been successfully placed and paid.",
  "isPaymentSuccess": true,
  "timestamp": "2024-05-18T12:00:10Z"
}
```

### 3. Kubernetes Testing

#### Deploy to Kubernetes

```bash
# Build images
docker build -t order-service:latest ./OrderService
docker build -t payment-service:latest ./PaymentService
docker build -t notification-service:latest ./NotificationService

# Load into Minikube
minikube image load order-service:latest
minikube image load payment-service:latest
minikube image load notification-service:latest

# Apply manifests
kubectl apply -f OrderService/kubernetes/kafka-deployment.yaml
kubectl apply -f OrderService/kubernetes/order-service-deployment.yaml
kubectl apply -f PaymentService/kubernetes/payment-service-deployment.yaml
kubectl apply -f NotificationService/kubernetes/notification-service-deployment.yaml

# Wait for deployments to be ready
kubectl wait --for=condition=available --timeout=300s \
  deployment/order-service \
  deployment/payment-service \
  deployment/kafka
```

#### Verify Kubernetes Setup

```bash
# Check deployments
kubectl get deployments

# Check pods
kubectl get pods

# Check services
kubectl get svc

# Describe deployment
kubectl describe deployment order-service

# View logs
kubectl logs deployment/order-service
kubectl logs deployment/kafka
```

#### Test Kubernetes Services

```bash
# Port forward to local machine
kubectl port-forward svc/order-service 5000:5000

# In another terminal, run test
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust-k8s-001",
    "amount": 200.00,
    "items": [
      {
        "productId": "prod-k8s-001",
        "quantity": 1,
        "price": 200.00
      }
    ]
  }'
```

#### Test Kafka in Kubernetes

```bash
# Port forward Kafka
kubectl port-forward svc/kafka 9092:9092

# Connect with Kafka consumer
kafka-console-consumer \
  --topic order-notifications \
  --from-beginning \
  --bootstrap-server localhost:9092
```

### 4. Load Testing

#### Using Apache Bench

```bash
# Create order
ab -n 100 -c 10 -p order.json -T application/json \
  http://localhost:5000/api/orders
```

order.json:
```json
{
  "customerId": "cust-load-test",
  "amount": 99.99,
  "items": [
    {
      "productId": "prod-001",
      "quantity": 1,
      "price": 99.99
    }
  ]
}
```

#### Using wrk (Lua-based)

```bash
# Install: https://github.com/wg/wrk

wrk -t12 -c400 -d30s \
  -s request.lua \
  http://localhost:5000/api/orders
```

request.lua:
```lua
request = function()
  wrk.method = "POST"
  wrk.headers["Content-Type"] = "application/json"
  wrk.body = '{"customerId":"cust-wrk","amount":99.99,"items":[{"productId":"prod","quantity":1,"price":99.99}]}'
  return wrk.format(nil)
end
```

### 5. Error Scenario Testing

#### Test 5.1: Payment Service Unavailable

```bash
# Stop payment service
docker-compose stop payment-service

# Try to create order
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust-error-test",
    "amount": 100.00,
    "items": []
  }'

# Expected: 503 Service Unavailable
# "error": "Payment service unavailable"

# Restart payment service
docker-compose start payment-service
```

#### Test 5.2: Kafka Unavailable

```bash
# Stop Kafka
docker-compose stop kafka

# Try to create order (should fail gracefully)
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust-kafka-test",
    "amount": 100.00,
    "items": []
  }'

# Note: Order may still succeed (Kafka notification failure is non-blocking)

# Restart Kafka
docker-compose start kafka
```

### 6. Circuit Breaker Testing

#### Simulate Circuit Breaker Activation

```bash
# Create a broken endpoint on payment service
# Then send multiple requests to trigger circuit breaker

# Send 10 requests rapidly
for i in {1..10}; do
  curl -X POST http://localhost:5000/api/orders \
    -H "Content-Type: application/json" \
    -d '{"customerId":"cust'$i'","amount":100,"items":[]}'
done

# After 5 failures, circuit breaker opens
# Next requests should return 503 immediately without waiting
```

### 7. Integration Test Example (C#/xUnit)

```csharp
[Fact]
public async Task CreateOrder_ValidOrder_ReturnsSuccessfulResponse()
{
    // Arrange
    var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
    var order = new
    {
        customerId = "test-customer",
        amount = 99.99m,
        items = new[] {
            new {
                productId = "prod-001",
                quantity = 1,
                price = 99.99m
            }
        }
    };

    var content = new StringContent(
        JsonSerializer.Serialize(order),
        Encoding.UTF8,
        "application/json"
    );

    // Act
    var response = await client.PostAsync("/api/orders", content);

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    
    var responseContent = await response.Content.ReadAsStringAsync();
    Assert.Contains("PaymentSucceeded", responseContent);
}

[Fact]
public async Task CreateOrder_InvalidData_ReturnsBadRequest()
{
    // Arrange
    var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
    var order = new { customerId = "", amount = 0m, items = new[] { } };

    var content = new StringContent(
        JsonSerializer.Serialize(order),
        Encoding.UTF8,
        "application/json"
    );

    // Act
    var response = await client.PostAsync("/api/orders", content);

    // Assert
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}
```

### 8. Performance Metrics

#### CPU & Memory Usage

```bash
# Docker Compose
docker stats mart_order-service_1

# Kubernetes
kubectl top pod
kubectl top node
```

#### Response Time

```bash
# Using curl with timing
curl -w "@curl-format.txt" -o /dev/null -s \
  -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{...}'
```

curl-format.txt:
```
    time_namelookup:  %{time_namelookup}\n
       time_connect:  %{time_connect}\n
    time_appconnect:  %{time_appconnect}\n
   time_pretransfer:  %{time_pretransfer}\n
      time_redirect:  %{time_redirect}\n
 time_starttransfer:  %{time_starttransfer}\n
                    ----------\n
         time_total:  %{time_total}\n
```

## Test Checklist

- [ ] Local Docker Compose tests pass
- [ ] Kafka message verification successful
- [ ] Kubernetes deployment successful
- [ ] Health checks responding correctly
- [ ] Error scenarios handled gracefully
- [ ] Circuit breaker activating as expected
- [ ] Load tests completed without issues
- [ ] Response times acceptable
- [ ] CPU/Memory usage within limits
- [ ] Logs show proper error tracking

## Cleanup

### Docker Compose

```bash
docker-compose down -v
```

### Kubernetes

```bash
kubectl delete deployment order-service payment-service notification-service
kubectl delete statefulset kafka zookeeper
kubectl delete svc order-service payment-service notification-service kafka zookeeper
```

## Troubleshooting

### Order Service Won't Start

```bash
# Check logs
docker logs mart_order-service_1

# Verify Kafka connectivity
curl -X GET http://localhost:9092/metrics

# Check network
docker network ls
docker network inspect mart_mart-network
```

### Kafka Consumer Not Reading Messages

```bash
# Check Kafka topics
kafka-topics --list --bootstrap-server localhost:9092

# Check consumer groups
kafka-consumer-groups --list --bootstrap-server localhost:9092

# Describe topic
kafka-topics --describe --topic order-notifications --bootstrap-server localhost:9092
```

### Kubernetes Pod Stuck in Pending

```bash
# Check pod details
kubectl describe pod order-service-xyz

# Check events
kubectl get events --sort-by='.lastTimestamp'

# Check resources
kubectl describe nodes
```
