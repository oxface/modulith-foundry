---
status: accepted
---

# Defer PostgreSQL row-level security

The reference implementation will not configure PostgreSQL row-level security. It will enforce discriminator tenancy through required tenant keys, tenant-scoped indexes, EF query filters, write validation, module contracts, and cross-tenant integration tests. An adopting product remains responsible for adding RLS when its threat model justifies the operational complexity; the template guidance will identify the extension point and require proof against connection pooling, transaction retries, migrations, administrative access, background workers, and shared transactions.
