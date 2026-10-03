---
status: accepted
---

# Use Entra External ID for production customers

The independent SaaS deployment will use a Microsoft Entra External ID external tenant for customer authentication, while Keycloak remains the local development identity provider. The product identifies an external identity by immutable issuer and subject, owns organization membership and authorization, and does not translate identity-provider groups into product permissions. Workforce-tenant B2B collaboration remains appropriate only if the product changes into one enterprise's partner portal.
