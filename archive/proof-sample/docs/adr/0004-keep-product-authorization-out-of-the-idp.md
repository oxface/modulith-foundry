---
status: accepted
---

# Keep product authorization outside the identity provider

Keycloak locally and the eventual production identity provider will establish external identity only. The product maps immutable issuer-and-subject identities to users and owns organization membership, permissions, approval limits, delegation, and business authorization so that authorization semantics remain part of the product and survive a change of identity provider.
