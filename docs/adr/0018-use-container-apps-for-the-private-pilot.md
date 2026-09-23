---
status: accepted
---

# Use Container Apps for the private pilot

The first Azure deployment uses one Azure Container Apps Consumption application plus a finite migration job and the cheapest feasible managed PostgreSQL, Service Bus, Redis, registry, identity, secret, and telemetry resources. This restricted non-HA pilot proves packaging, managed-service compatibility, restore, rollback, observability, and spend controls without making AKS, k3s, Flux, or a public gateway part of v1. Kubernetes and the production edge remain separate decisions that require a demonstrated operational or threat-model driver.
