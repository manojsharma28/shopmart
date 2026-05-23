# OrderService - Kubernetes Deployment Guide

## Prerequisites

- Docker installed and running
- Kubernetes cluster (Minikube, Docker Desktop Kubernetes, or cloud provider)
- kubectl configured to access your cluster
- All services built as Docker images

## Local Development with Docker Compose

To run the services locally with Kafka:

```bash
cd d:\MyData\Mart
docker-compose up -d
```

This will start:
- Zookeeper (for Kafka coordination)
- Kafka (message broker)
- Order Service (port 5000)
- Payment Service (port 5001)
- Notification Service

## Building Docker Images

### Build Order Service Image

```bash
cd d:\MyData\Mart\OrderService
docker build -t order-service:latest .
```

### Build Payment Service Image

```bash
cd d:\MyData\Mart\PaymentService
docker build -t payment-service:latest .
```

### Build Notification Service Image

```bash
cd d:\MyData\Mart\NotificationService
docker build -t notification-service:latest .
```

## Kubernetes Deployment

### 1. Load Images into Kubernetes (Minikube)

If using Minikube, load images from Docker:

```bash
minikube image load order-service:latest
minikube image load payment-service:latest
minikube image load notification-service:latest
```

### 2. Deploy Kafka and Zookeeper

```bash
kubectl apply -f OrderService/kubernetes/kafka-deployment.yaml
```

Verify Kafka is running:

```bash
kubectl get pods
kubectl logs -f deployment/kafka
```

### 3. Deploy Services

Deploy Order Service:

```bash
kubectl apply -f OrderService/kubernetes/order-service-deployment.yaml
```

Deploy Payment Service:

```bash
# Create payment-service-deployment.yaml if not exists
kubectl apply -f PaymentService/kubernetes/payment-service-deployment.yaml
```

Deploy Notification Service:

```bash
# Create notification-service-deployment.yaml if not exists
kubectl apply -f NotificationService/kubernetes/notification-service-deployment.yaml
```

### 4. Verify Deployments

```bash
# Check all deployments
kubectl get deployments

# Check pods
kubectl get pods

# Check services
kubectl get services

# View logs
kubectl logs -f deployment/order-service
```

## Testing the Order Service

### 1. Port Forward (Local Testing)

```bash
kubectl port-forward svc/order-service 5000:5000
```

### 2. Create an Order

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust123",
    "amount": 99.99,
    "items": [
      {
        "productId": "prod001",
        "quantity": 2,
        "price": 49.99
      }
    ]
  }'
```

### 3. Check Order Status

```bash
curl http://localhost:5000/api/orders/order-id-here
```

## Service Communication Flow

```
Client
  ↓
OrderService /api/orders
  ↓
PaymentService (HTTP) - Validates payment
  ↓
If Payment succeeds/fails:
  ↓
Kafka Topic (order-notifications)
  ↓
NotificationService (Kafka Consumer) - Sends notification
```

## Monitoring and Logging

### View Service Logs

```bash
# Order Service
kubectl logs -f deployment/order-service -c order-service

# Payment Service
kubectl logs -f deployment/payment-service -c payment-service

# Notification Service
kubectl logs -f deployment/notification-service -c notification-service

# Kafka
kubectl logs -f statefulset/kafka
```

### Check Kafka Topics

```bash
# Access Kafka pod
kubectl exec -it kafka-0 /bin/bash

# List topics
kafka-topics --list --bootstrap-server localhost:9092

# Describe topic
kafka-topics --describe --topic order-notifications --bootstrap-server localhost:9092

# View messages
kafka-console-consumer --topic order-notifications --from-beginning --bootstrap-server localhost:9092
```

## Scaling Services

### Scale Order Service

```bash
kubectl scale deployment/order-service --replicas=3
```

### Scale Payment Service

```bash
kubectl scale deployment/payment-service --replicas=2
```

## Update Deployments

### Rolling Update

```bash
# Update image
kubectl set image deployment/order-service order-service=order-service:v2

# Monitor rollout
kubectl rollout status deployment/order-service

# Rollback if needed
kubectl rollout undo deployment/order-service
```

## Environment Variables Configuration

Services read configuration from environment variables set in deployment.yaml:

- `ASPNETCORE_ENVIRONMENT`: Deployment environment (Development/Production)
- `Kafka__BootstrapServers`: Kafka bootstrap server address
- `PaymentService__Url`: Payment Service URL (for Order Service)

## Cleanup

### Stop Docker Compose

```bash
docker-compose down
```

### Delete Kubernetes Resources

```bash
# Delete all services
kubectl delete deployment order-service
kubectl delete deployment payment-service
kubectl delete deployment notification-service

# Delete Kafka
kubectl delete statefulset kafka
kubectl delete statefulset zookeeper
kubectl delete service kafka zookeeper

# Delete all
kubectl delete -f OrderService/kubernetes/
```

## Production Considerations

1. **Kafka**: Use managed Kafka (AWS MSK, Confluent Cloud) or Kafka Helm chart for high availability
2. **Service Mesh**: Consider Istio or Linkerd for advanced traffic management
3. **Persistence**: Add PersistentVolumes for Kafka and Zookeeper
4. **Monitoring**: Integrate Prometheus and Grafana
5. **Security**: Use Network Policies, RBAC, and Secrets for sensitive data
6. **Image Registry**: Push to Docker Hub, ECR, or private registry instead of local images
7. **Health Checks**: Implement comprehensive health check endpoints
8. **Configuration Management**: Use ConfigMaps for non-sensitive config, Secrets for credentials
