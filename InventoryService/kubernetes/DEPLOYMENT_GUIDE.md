# InventoryService - Kubernetes Deployment Guide

## Prerequisites

- Docker installed and running
- Kubernetes cluster (minikube, k3s, or cloud provider)
- kubectl configured to access your cluster
- Docker registry access (Docker Hub or private registry)

## Architecture Overview

The InventoryService deployment includes:
- **Deployment**: 2 replicas for high availability
- **Service**: ClusterIP for internal DNS discovery (inventory-service:5003)
- **Health Probes**: Liveness and readiness checks
- **Resource Limits**: Memory and CPU constraints

## Step 1: Build Docker Image

From the root directory of the project:

```bash
# Build the InventoryService Docker image
docker build -f InventoryService/Dockerfile -t inventory-service:latest .

# If using a registry (e.g., Docker Hub)
docker tag inventory-service:latest your-registry/inventory-service:latest
docker push your-registry/inventory-service:latest
```

## Step 2: Ensure Kafka is Deployed

InventoryService depends on Kafka. If not already deployed:

```bash
# Apply Kafka deployment (from OrderService kubernetes files)
kubectl apply -f OrderService/kubernetes/kafka-deployment.yaml

# Verify Kafka is running
kubectl get statefulsets kafka
kubectl get pods -l app=kafka
```

Wait for Kafka pod to be ready:
```bash
kubectl wait --for=condition=Ready pod -l app=kafka --timeout=300s
```

## Step 3: Deploy InventoryService

```bash
# Apply the deployment
kubectl apply -f InventoryService/kubernetes/inventory-service-deployment.yaml

# Verify deployment
kubectl get deployments inventory-service
kubectl get pods -l app=inventory-service
kubectl get services inventory-service
```

Expected output:
```
NAME                    READY   UP-TO-DATE   AVAILABLE   AGE
inventory-service       2/2     2            2           10s

NAME                               READY   STATUS    RESTARTS   AGE
inventory-service-xxxxx-xxxxx      1/1     Running   0          10s
inventory-service-yyyyy-yyyyy      1/1     Running   0          8s

NAME                TYPE        CLUSTER-IP     EXTERNAL-IP   PORT(S)    AGE
inventory-service   ClusterIP   10.0.0.100     <none>        5003/TCP   10s
```

## Step 4: Verify Health Checks

```bash
# Port forward to test locally
kubectl port-forward service/inventory-service 5003:5003

# In another terminal, test health endpoints
curl http://localhost:5003/health
curl http://localhost:5003/health/ready

# Expected responses
# {"status":"healthy","timestamp":"2024-05-18T12:00:00Z"}
# {"status":"ready","timestamp":"2024-05-18T12:00:00Z"}
```

## Step 5: Verify Kafka Integration

```bash
# Check logs to ensure Kafka consumer is running
kubectl logs -l app=inventory-service -f

# Expected log output
# [INF] Started consuming from Kafka topic: order-notifications
# [INF] InventoryService started on port 8080
```

## Testing the Deployment

### Test 1: Get All Products

```bash
curl http://localhost:5003/api/inventory/products -H "Accept: application/json"
```

### Test 2: Check Stock Availability

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

### Test 3: Reserve Stock

```bash
curl -X POST http://localhost:5003/api/inventory/reserve \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "test-order-001",
    "items": [{"productId": "prod001", "quantity": 3}]
  }'
```

### Test 4: Confirm Reservation

```bash
curl -X POST http://localhost:5003/api/inventory/confirm/test-order-001
```

## Monitoring & Debugging

### View Logs

```bash
# All InventoryService logs
kubectl logs -l app=inventory-service -f

# Specific pod logs
kubectl logs inventory-service-xxxxx

# Tail with timestamps
kubectl logs -l app=inventory-service -f --timestamps=true
```

### Describe Pod

```bash
# Get detailed pod information
kubectl describe pod -l app=inventory-service

# Shows: events, resource usage, mounts, environment variables
```

### Execute Commands in Pod

```bash
# Open shell in pod
kubectl exec -it inventory-service-xxxxx -- /bin/bash

# Run commands
dotnet --version
ls -la /app
```

### Check Resource Usage

```bash
# View current resource usage
kubectl top pod -l app=inventory-service

# View resource requests vs usage
kubectl get pods -l app=inventory-service --show-labels -o wide
```

## Scaling

### Manual Scaling

```bash
# Scale to 3 replicas
kubectl scale deployment inventory-service --replicas=3

# Verify
kubectl get pods -l app=inventory-service

# Scale back to 2
kubectl scale deployment inventory-service --replicas=2
```

### Horizontal Pod Autoscaling

First, ensure metrics-server is deployed:

```bash
# Check if metrics-server exists
kubectl get deployment metrics-server -n kube-system

# If not, install it (for minikube)
minikube addons enable metrics-server

# For other clusters, install from:
# https://github.com/kubernetes-sigs/metrics-server
```

Then create HPA:

```bash
kubectl autoscale deployment inventory-service \
  --min=2 \
  --max=5 \
  --cpu-percent=80

# View HPA status
kubectl get hpa inventory-service
```

## Integration with Other Services

### OrderService Integration

OrderService should call InventoryService to check and reserve stock:

```bash
# From OrderService
curl -X POST http://inventory-service:5003/api/inventory/reserve \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "order-123",
    "items": [{"productId": "prod001", "quantity": 2}]
  }'
```

Configure OrderService environment variable:
```yaml
env:
- name: InventoryService__Url
  value: "http://inventory-service:5003/"
```

### Kafka Topic Configuration

InventoryService subscribes to `order-notifications` topic published by OrderService:

```yaml
env:
- name: Kafka__OrderNotificationTopic
  value: "order-notifications"
- name: Kafka__ConsumerGroupId
  value: "inventory-service-group"
```

## Updating the Deployment

### Update Image

```bash
# Build and push new image
docker build -f InventoryService/Dockerfile -t your-registry/inventory-service:v1.1 .
docker push your-registry/inventory-service:v1.1

# Update deployment
kubectl set image deployment/inventory-service \
  inventory-service=your-registry/inventory-service:v1.1

# Monitor rollout
kubectl rollout status deployment/inventory-service
```

### Rollback on Error

```bash
# View rollout history
kubectl rollout history deployment/inventory-service

# Rollback to previous version
kubectl rollout undo deployment/inventory-service

# Rollback to specific revision
kubectl rollout undo deployment/inventory-service --to-revision=2
```

## Troubleshooting

### Pod Not Starting

```bash
# Check pod events
kubectl describe pod inventory-service-xxxxx

# Check logs for errors
kubectl logs inventory-service-xxxxx --previous

# Common issues:
# - Image pull error: Verify image name and registry credentials
# - CrashLoopBackOff: Check application startup errors in logs
# - ImagePullBackOff: Check image exists and is accessible
```

### Kafka Connection Issues

```bash
# Verify Kafka service is accessible
kubectl exec -it inventory-service-xxxxx -- \
  curl kafka:9092

# Check Kafka service DNS
kubectl exec -it inventory-service-xxxxx -- \
  nslookup kafka

# Verify Kafka is running
kubectl get pods -l app=kafka
```

### Health Probe Failures

```bash
# Check if health endpoint responds
kubectl exec -it inventory-service-xxxxx -- \
  curl localhost:8080/health

# If it times out or fails:
# 1. Check application logs
# 2. Verify port is correct (8080 in container)
# 3. Check probe configuration in YAML
```

### High Memory Usage

```bash
# Check current usage
kubectl top pod -l app=inventory-service

# If exceeding limits:
# 1. Increase memory limit in deployment YAML
# 2. Optimize application code
# 3. Consider caching strategy
```

## Performance Tuning

### Resource Configuration

Current limits in deployment.yaml:
```yaml
resources:
  requests:
    memory: "256Mi"
    cpu: "100m"
  limits:
    memory: "512Mi"
    cpu: "500m"
```

Adjust based on load testing:
```yaml
# For high-traffic scenarios
requests:
  memory: "512Mi"
  cpu: "250m"
limits:
  memory: "1024Mi"
  cpu: "1000m"
```

### Probe Configuration

Current settings:
```yaml
livenessProbe:
  initialDelaySeconds: 10
  periodSeconds: 15

readinessProbe:
  initialDelaySeconds: 5
  periodSeconds: 10
```

For slow startup applications, increase `initialDelaySeconds`.

## Production Checklist

- [ ] Image built and pushed to registry
- [ ] Kafka deployment verified running
- [ ] InventoryService pods in Running state
- [ ] Health checks passing
- [ ] Kafka consumer logs showing subscription
- [ ] OrderService configured to call InventoryService
- [ ] Resource requests/limits appropriate for workload
- [ ] Monitoring and alerting configured
- [ ] Log aggregation set up (ELK, Loki, etc.)
- [ ] Backup strategy for persistent data (if using DB)
- [ ] Disaster recovery plan documented

## Cleanup

To remove InventoryService from cluster:

```bash
kubectl delete deployment inventory-service
kubectl delete service inventory-service

# Verify removal
kubectl get deployments
kubectl get services
```

## Next Steps

1. Integrate with OrderService to check/reserve inventory
2. Add OrderService configuration for InventoryService endpoint
3. Set up monitoring and alerting
4. Implement persistence layer (database) for production
5. Add unit and integration tests
6. Configure CI/CD pipeline for automated deployments
