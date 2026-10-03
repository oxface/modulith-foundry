---
status: accepted
---

# Use a BFF with server-side sessions

The browser will authenticate through a backend-for-frontend using secure cookies, while the complete authentication ticket is stored server-side to support multiple replicas and administrative revocation. Redis may hold authentication tickets and application cache entries, but Data Protection keys are a separate durable concern: Azure production will persist the key ring in Blob Storage protected by Key Vault, while local development uses Aspire-managed secrets and local development key handling.
