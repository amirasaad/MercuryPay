# MercuryPay Kubernetes Deployment Guide

## Overview

This directory contains complete Kubernetes manifests for deploying the MercuryPay microservices platform. The manifests follow production best practices including:

- **Service isolation** in a dedicated namespace
- **Database-per-service** pattern with separate PostgreSQL databases
- **High availability** with multiple replicas and pod anti-affinity rules
- **Security** with RBAC, NetworkPolicy, and restrictive pod security contexts
- **Observability** with OpenTelemetry instrumentation
- **Load balancing** via Ingress controller
- **Resilience** with liveness/readiness probes and pod disruption budgets

## Prerequisites

Before deploying, ensure:

1. **Kubernetes cluster** (v1.24+) running and accessible via `kubectl`
2. **Storage classes** available (default storage class for PVCs):

   ```bash
   kubectl get storageclass
   ```

3. **Ingress controller** installed (nginx or similar):

   ```bash
   kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.1/deploy/static/provider/cloud/deploy.yaml
   ```

4. **Container images** built and pushed to a registry:
   - `mercurypay/payment-service:latest`
   - `mercurypay/wallet-service:latest`
   - `mercurypay/lending-service:latest`
   - `mercurypay/risk-service:latest`
   - `mercurypay/api-gateway:latest`

## Deployment Steps

### Step 1: Apply Namespace and Configuration

Apply the namespace, secrets, and ConfigMaps:

```bash
kubectl apply -f k8s/00-namespace.yaml
kubectl apply -f k8s/01-secrets-configmap.yaml
```

**Important**: Review and update secrets before production:

```bash
kubectl edit secret mercurypay-secrets -n mercurypay
```

Replace these values:

- `POSTGRES_PASSWORD`: Change to a strong password
- `REDIS_PASSWORD`: Change to a strong password
- `JWT_SECRET_KEY`: Change to a secure value

### Step 2: Deploy Infrastructure Services

Deploy PostgreSQL and Redis:

```bash
kubectl apply -f k8s/02-postgres.yaml
kubectl apply -f k8s/03-redis.yaml
```

Wait for both to be ready:

```bash
kubectl wait --for=condition=ready pod -l app=postgres -n mercurypay --timeout=300s
kubectl wait --for=condition=ready pod -l app=redis -n mercurypay --timeout=300s
```

Verify:

```bash
kubectl get statefulsets -n mercurypay
kubectl get pvc -n mercurypay
```

### Step 3: Apply RBAC and Network Policies

```bash
kubectl apply -f k8s/09-rbac-network-policy.yaml
```

### Step 4: Deploy Microservices

Deploy all microservices:

```bash
kubectl apply -f k8s/04-payment-service.yaml
kubectl apply -f k8s/05-wallet-service.yaml
kubectl apply -f k8s/06-lending-service.yaml
kubectl apply -f k8s/07-risk-service.yaml
kubectl apply -f k8s/08-api-gateway.yaml
```

Wait for all to be ready:

```bash
kubectl wait --for=condition=ready pod -l app=payment-service -n mercurypay --timeout=300s
kubectl wait --for=condition=ready pod -l app=wallet-service -n mercurypay --timeout=300s
kubectl wait --for=condition=ready pod -l app=lending-service -n mercurypay --timeout=300s
kubectl wait --for=condition=ready pod -l app=risk-service -n mercurypay --timeout=300s
kubectl wait --for=condition=ready pod -l app=api-gateway -n mercurypay --timeout=300s
```

### Step 5: Configure Resilience Policies

```bash
kubectl apply -f k8s/11-pod-disruption-budgets.yaml
```

### Step 6: Configure Ingress and DNS

Update `k8s/10-ingress.yaml` to match your domain (replace `mercurypay.example.com`):

```bash
sed -i 's/mercurypay.example.com/your-domain.com/g' k8s/10-ingress.yaml
kubectl apply -f k8s/10-ingress.yaml
```

Get the Ingress IP/hostname:

```bash
kubectl get ingress -n mercurypay
```

Add DNS record:

```
your-domain.com  ->  <ingress-ip-or-hostname>
```

## Verification

### Check all resources

```bash
kubectl get all -n mercurypay
kubectl get pvc -n mercurypay
kubectl get ingress -n mercurypay
```

### Check service readiness

```bash
kubectl get pods -n mercurypay -o wide
kubectl logs -n mercurypay -f deployment/payment-service
```

### Test API Gateway

```bash
kubectl port-forward svc/api-gateway 8080:80 -n mercurypay
curl http://localhost:8080/health
```

### Test inter-service communication

```bash
kubectl exec -it deployment/payment-service -n mercurypay -- \
  curl http://wallet-service.mercurypay.svc.cluster.local/health
```

## Scaling

### Scale a microservice

```bash
kubectl scale deployment payment-service --replicas=3 -n mercurypay
```

### Monitor scaling

```bash
kubectl get deployment payment-service -n mercurypay -w
```

## Monitoring & Logs

### View logs for a service

```bash
kubectl logs -f deployment/payment-service -n mercurypay
```

### Follow all logs across namespace

```bash
kubectl logs -f -l app=payment-service -n mercurypay --all-containers=true
```

### Check resource usage

```bash
kubectl top pods -n mercurypay
kubectl top nodes
```

### Get pod events

```bash
kubectl describe pod <pod-name> -n mercurypay
kubectl get events -n mercurypay --sort-by='.lastTimestamp'
```

## Updating Deployments

### Update image for a service

```bash
kubectl set image deployment/payment-service \
  payment-service=mercurypay/payment-service:v1.1.0 \
  -n mercurypay
```

### Check rollout status

```bash
kubectl rollout status deployment/payment-service -n mercurypay
```

### Rollback if needed

```bash
kubectl rollout undo deployment/payment-service -n mercurypay
```

## Environment Variables & Configuration

Services consume configuration from:

1. **ConfigMap** (`mercurypay-config`): Non-sensitive config (service URLs, Redis host, OTEL endpoints)
2. **Secrets** (`mercurypay-secrets`): Sensitive values (database passwords, JWT key, etc.)
3. **Deployment env blocks**: Override-specific values per service

To add a new environment variable:

1. Add it to `k8s/01-secrets-configmap.yaml` (ConfigMap or Secret)
2. Reference it in the Deployment's `env:` section using `valueFrom`
3. Redeploy: `kubectl apply -f k8s/01-secrets-configmap.yaml && kubectl rollout restart deployment/<service> -n mercurypay`

## Troubleshooting

### Pods stuck in Pending

```bash
kubectl describe pod <pod-name> -n mercurypay
# Check if storage is available or image pull issues
```

### Connection refused errors between services

```bash
# Check DNS resolution
kubectl exec -it <pod> -n mercurypay -- nslookup redis.mercurypay.svc.cluster.local
# Check network policies
kubectl describe networkpolicy -n mercurypay
```

### Database not initializing

```bash
kubectl logs statefulset/postgres -n mercurypay
# Check if init script in ConfigMap is mounted
kubectl describe pod postgres-0 -n mercurypay
```

### Services crashing (CrashLoopBackOff)

```bash
kubectl logs deployment/payment-service -n mercurypay --tail=50
# Check environment variables are correctly injected
kubectl exec deployment/payment-service -n mercurypay -- env | grep Redis
```

## Cleanup

To remove the entire deployment:

```bash
kubectl delete namespace mercurypay
```

This will delete all resources within the namespace, including PVCs and all data.

## Production Considerations

Before deploying to production:

1. **Use sealed secrets** or external secret management (Vault, AWS Secrets Manager)
2. **Enable pod autoscaling** with HPA (Horizontal Pod Autoscaler)
3. **Set up monitoring & alerting** (Prometheus, Grafana, AlertManager)
4. **Configure backup strategy** for PostgreSQL persistent volumes
5. **Use image registries** with authentication and signed images
6. **Enable TLS** for all inter-service communication (mTLS)
7. **Implement resource quotas** and LimitRanges per namespace
8. **Set up log aggregation** (ELK, Loki, etc.)
9. **Use service mesh** (Istio, Linkerd) for advanced traffic management
10. **Document your disaster recovery plan** and test regularly
