---
status: accepted
---

# Use product invitations with OIDC-controlled admission

Access owns organization invitations and memberships but never provisions ordinary identity-provider accounts. One OIDC/JIT application flow supports both open-registration providers and directory-gated providers whose administrators pre-provision, federate, or assign users; `(issuer, subject)` is the durable external identity and a provider rejection leaves the product invitation pending. Invitation acceptance requires a matching provider-verified email and otherwise fails closed until a deployment explicitly adds another assurance path. Keycloak Admin REST, Microsoft Graph provisioning, SCIM, and automatic email-based account linking are deferred until a named deployment requires them.
