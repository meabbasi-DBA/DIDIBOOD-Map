# DevOps Rules

- Infrastructure must be reproducible
- Use Infrastructure as Code
- Avoid manual deployments
- Every service must be observable
- Use health checks everywhere
- Support rolling deployments
- Design stateless services when possible
- Secrets must never be hardcoded
- Use environment isolation
- Ensure disaster recovery readiness

# Deployment Standards

## Containers
- Use minimal base images
- Optimize image size
- Avoid unnecessary layers
- Use non-root containers

## Kubernetes
- Use readiness probes
- Use liveness probes
- Configure resource limits
- Configure autoscaling

## CI/CD
- Automated testing required
- Deployment must be repeatable
- Validate before production release

## Monitoring
- Centralized logging required
- Metrics collection mandatory
- Alerting required for critical systems