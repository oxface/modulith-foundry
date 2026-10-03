---
status: accepted
---

# Use discriminator-based multitenancy

The product will serve multiple organizations from one deployment and database by carrying a tenant discriminator on every tenant-owned record, event, projection, message, and audit entry. Schema-per-tenant is rejected because schemas already express module ownership and multiplying migrations by both module and tenant would add operational cost; stronger database isolation remains a possible later tier for customers who require it.

