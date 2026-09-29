# Access module charter

## Purpose

Access establishes who a person is to the product, which organizations they may enter, and which product-defined roles they hold there. It supplies current actor and organization access to business modules without becoming the owner of their business policy.

## Owned concepts and data

- `Organization`, including its immutable canonical slug and exceptional slug aliases.
- `User` and its links to immutable external `(issuer, subject)` identities.
- `Membership`, Organization `Invitation`, system-role assignments, and membership lifecycle state.
- Access-owned role definitions and the assembled catalog of roles contributed by business modules.
- Access-schema audit entries and the invitation-email outbox.

Access audit entries are immutable. Indexed relational envelope fields identify the organization, actor, action, subject, and occurrence time; a versioned `jsonb` details document records the action-specific facts needed to explain the historical action without serializing whole entities or secrets.

The `access` PostgreSQL schema is authoritative. Keycloak and Entra authenticate principals but do not own these records.

On a validated OIDC callback, Access resolves the exact `(issuer, subject)` link or creates one User and link atomically. Repeated authentication updates mutable profile data without changing the User identity; concurrent first authentication is constrained to one link and one User. Provider tokens, authentication cookies, and provider SDK types remain outside the module.

V1 performs no automatic deletion of Organizations, Users, Memberships, or accepted Invitations. Expired invitation payloads and operational email-outbox data may be purged under explicit jobs; audit retention/redaction remains a compliance-driven adoption decision.

Membership suspension is reversible by an Organization Administrator and retains assigned roles.
Removal ends access and cannot be directly reactivated. A removed User may rejoin only through a
new matching Invitation; acceptance retains the removed Membership as history and creates a new
Membership identity with the Invitation's current assignments. At most one active or suspended
Membership may exist for one User and Organization.

## Interface

Commands exposed through Access capabilities:

- create an Organization and its first Organization Administrator membership;
- invite a person to an Organization with selected system roles;
- accept one valid Invitation for the authenticated external identity;
- change a Membership's system roles;
- suspend, reactivate, or remove a Membership.

Queries exposed through Access capabilities:

- link or resolve an authenticated external identity to a User;
- list the Organizations currently accessible to a User;
- resolve a route slug and current Membership into an immutable actor/tenant context;
- list an Organization's memberships, invitations, and assigned system roles.

Expected business failures are explicit: slug unavailable, invitation invalid/expired/consumed, identity mismatch, membership absent/inactive, permission denied, and last-administrator protection. No interface exposes Access entities, its DbContext, provider claims, or queryables.

## Integration and external effects

Access publishes no broker integration event in v1 because no accepted consumer needs one. Organization-invitation email is an Access-owned external effect: invitation state, audit, and an organization-invitation email-delivery record commit together; a native hosted worker sends it to Mailpit locally and the configured provider later.

The organization-invitation email outbox is concrete Access infrastructure, not the future broker-event outbox. Its delivery record is technical durable-process state, not a child of the Invitation aggregate. It uses a database lease for multi-replica dispatch, protects the recoverable bearer payload with the process Data Protection key ring, retries the same generation after ambiguous SMTP outcomes, and clears the protected payload after confirmed delivery or supersession. Explicit resend rotates the invitation secret and generation. `IEmailTransport` is the narrow public extension point for replacing SMTP; it is intentionally not a cross-module business contract, and no general notification framework exists in v1.

## Invariants

- Every active Organization has at least one active Organization Administrator.
- Organization slugs are normalized, globally unique, and immutable in normal workflows.
- `(issuer, subject)` identifies at most one User; email is not durable identity.
- An Invitation belongs to one Organization, is single-use and expiring, and can create at most one Membership.
- Only a current Organization Administrator may manage invitations, memberships, or access roles.
- Organization Administrator grants access administration only; it is not a Sales, Inventory, or Purchasing super-role.

## Authorization

V1 system roles use stable textual identifiers. Access defines `organization-administrator`, persists assignments, and validates assignments against the catalog assembled by the host. Sales, Inventory, and Purchasing expose their own role identifiers from their Contracts projects and remain authoritative for those roles' meaning and enforcement. This keeps role ownership aligned with module ownership while leaving Access responsible for membership access management. The catalog is a product composition input, not an authorization-provider API; a later OpenFGA adapter may map the same stable role and permission identifiers into its model and relationship tuples without moving business policy into Access.

Each business module registers its code-defined role and permission manifest through its normal
`Add{Module}Module` composition entry point. Access validates and assembles those manifests; the API
does not duplicate the catalog. Membership administration checks `access.members.manage` inside the
Access operation rather than trusting HTTP policy or a role cached in the authentication session.
Role replacement is an atomic full-set operation. It serializes on the owning Organization row
because the last-active-administrator invariant spans multiple Memberships; locking only the target
Membership cannot protect that invariant. Successful changes and security-significant denials are
recorded in the Access audit. Membership suspension and removal use the same serialization boundary;
suspended and removed Memberships do not grant access or permissions.
Ordinary membership administration lists only active and suspended Memberships. Removed tenures
remain available to audit and deliberately historical queries; lifecycle visibility is not hidden by
a global persistence filter.

## Explicit exclusions

- Creating or inviting identity-provider accounts through provider administration APIs.
- Tenant-defined roles, direct grants, denies, inheritance, and OpenFGA.
- Billing, subscription entitlement, Trial lifecycle, unrestricted-signup guarantees, custom domains, and Organization deletion.
- Business-object authorization, approval arithmetic, support impersonation, or machine identities.
