# OIDC onboarding modes and a later OpenFGA mapping

Status: Research input for architecture review; not an implementation decision record.

As of: 2026-09-23

## Executive conclusion

The application can support both open self-registration and administrator-controlled enterprise directories without changing its product model or calling an identity-provider administration API. Its portable boundary is ordinary OIDC: after validating the authorization-code callback, resolve a product identity by `(issuer, subject)`, JIT-create the product `User` when absent, and let the product own organizations, memberships, invitations, roles, and permissions. OIDC defines `sub` as an identifier local to an issuer and never reassigned within that issuer; it does not make email a durable identity key. [OpenID Connect Core 1.0, subject identifiers](https://openid.net/specs/openid-connect-core-1_0.html#SubjectIDTypes)

Whether a previously unknown person is able to reach that callback is a deployment policy of the configured IdP:

- Keycloak can expose realm self-registration, or disable it and admit only administrators' users, federated users, or users imported during an identity-broker first-login flow. [Keycloak 26.7.4 Server Administration Guide, registration](https://www.keycloak.org/docs/latest/server_admin/#_registration)
- A Microsoft Entra External ID **external tenant** is Microsoft's current CIAM option and supplies sign-up/sign-in user flows for customer and business-customer applications. Azure AD B2C is no longer sold to new customers as of 1 May 2025, so it should not be the new-product recommendation. [External tenant overview](https://learn.microsoft.com/en-us/entra/external-id/customers/overview-customers-ciam) [Azure AD B2C FAQ](https://learn.microsoft.com/en-us/azure/active-directory-b2c/faq)
- A Microsoft Entra **workforce tenant** can restrict an OIDC enterprise application to assigned users/groups. It can also represent partners as B2B guest objects whose credentials remain at their home identity provider. These are directory-administration controls, not product organization memberships. [Enterprise application properties](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/application-properties#assignment-required) [B2B collaboration overview](https://learn.microsoft.com/en-us/entra/external-id/what-is-b2b)

For v1, implement one application invitation/JIT flow and document two supported deployment policies: **open registration** and **directory-gated authentication**. Do not add Keycloak Admin REST, Microsoft Graph invitation/provisioning, SCIM, home-realm routing, or an IdP abstraction beyond the OIDC claim normalization actually needed by tested deployments.

The proposed code-defined system roles also preserve a clean OpenFGA adoption path. OpenFGA explicitly recommends representing built-in roles as relations in the authorization model and their assignments as relationship tuples; user-defined roles need the more elaborate `role` object/tuple pattern. [OpenFGA role-modeling guidance](https://openfga.dev/docs/best-practices/modeling-roles) [OpenFGA model design principles](https://openfga.dev/docs/best-practices/modeling-design-principles)

## Exact identity terminology

These similarly named Entra choices are not interchangeable:

| Term | Meaning relevant to this product |
| --- | --- |
| Entra External ID, external tenant | A separate customer/business-customer directory for external-facing applications. Its user flows provide self-service sign-up, sign-in, and password reset; signing up creates a customer user object in that directory. [External tenant overview](https://learn.microsoft.com/en-us/entra/external-id/customers/overview-customers-ciam) |
| Entra workforce tenant | The standard employee/internal-app directory. An enterprise application can require direct user/group assignment before Entra issues a token. [Manage access to apps](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/what-is-access-management#requiring-user-assignment-for-an-app) |
| Entra B2B collaboration | A workforce-tenant feature in which a partner is represented by a guest user object in the resource tenant but authenticates with a home work, school, social, or email-OTP identity. Entra can create that guest representation through invitation or B2B self-service sign-up. [B2B collaboration overview](https://learn.microsoft.com/en-us/entra/external-id/what-is-b2b) [Identity providers for workforce tenants](https://learn.microsoft.com/en-us/entra/external-id/identity-providers) |
| Entra federation in an external tenant | An external tenant can offer one or more upstream Entra tenants as custom OIDC identity providers in its sign-up/sign-in user flow. A first sign-in still creates a customer representation in the external tenant. [Add Entra federation for customer sign-in](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-entra-id-federation-customers) [Identity providers for external tenants](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-authentication-methods-customers) |
| Azure AD B2C | The legacy CIAM product. Existing customers can continue to use it, but it ceased being available to new customers on 1 May 2025; External ID external tenants are the forward-looking choice for a new product. [Azure AD B2C FAQ](https://learn.microsoft.com/en-us/azure/active-directory-b2c/faq) |

The application should call both Keycloak and whichever Entra tenant is selected an **OIDC provider**. It should not expose the provider's directory vocabulary (`guest`, `customer account`, realm organization, Entra group, or app role) in the domain model.

## Provider-neutral onboarding modes

### Mode A: open registration

The application creates its own organization invitation and emails its own bearer link. A recipient who lacks an IdP account follows the same OIDC challenge and uses the provider's self-service sign-up UI:

- Keycloak shows a registration link when realm self-registration is enabled. Keycloak recommends enabling `Verify email`; the current registration flow verifies the address before credential setup. [Keycloak registration documentation](https://www.keycloak.org/docs/latest/server_admin/#_registration)
- An Entra External ID external-tenant user flow supports email/password, email one-time passcode, and configured social or federated providers. Completing sign-up creates a user object and redirects the browser to the application with a token. [Create a customer sign-up/sign-in flow](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-user-flow-sign-up-sign-in-customers) [External-tenant authentication methods](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-authentication-methods-customers)

The product sees only the successful OIDC callback. It then creates `User`/`ExternalIdentity`, consumes the product invitation, and creates the organization membership in its own transaction.

### Mode B: directory-gated authentication

The same application invitation works when self-registration is disabled. The product still redirects through OIDC, but an unknown or unassigned person is rejected by the provider before returning to the product:

- Keycloak documents that disabling realm self-registration does not prevent administrators from adding users or identity brokering from importing users according to the configured first-login flow. The first-login flow can also be configured to allow only already-existing realm users. [Keycloak registration and broker first-login documentation](https://www.keycloak.org/docs/latest/server_admin/#_registration) [Keycloak first broker login](https://www.keycloak.org/docs/latest/server_admin/#_first_login_flow)
- In an Entra workforce tenant, setting `Assignment required` to `Yes` means only assigned users/groups can obtain a token for the application. [Enterprise application properties](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/application-properties#assignment-required)
- For external partners, an Entra administrator can first establish a B2B guest representation; the partner still uses credentials managed by its home provider. [B2B collaboration overview](https://learn.microsoft.com/en-us/entra/external-id/what-is-b2b) [B2B guest properties](https://learn.microsoft.com/en-us/entra/external-id/user-properties)

There is no portable OIDC operation for the product to discover whether an email address has a provider account, create one, or assign it to an enterprise application: OIDC standardizes authentication requests, tokens, UserInfo, and claims, not directory lifecycle administration. [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html) Those directory operations are provider administration. In this mode, directory administrators pre-provision/admit users out of band; product administrators manage product invitations and memberships. A failed IdP admission must leave the product invitation pending and show provider-neutral recovery guidance.

### Mode C: enterprise federation behind the CIAM provider

An Entra External ID external tenant can expose one or more upstream Entra workforce tenants as custom OIDC providers. Users authenticate with their organizational credentials while the product still trusts one configured external-tenant issuer. Microsoft documents multiple upstream Entra tenants as separately configured OIDC identity providers in the external-tenant user flow. [Entra federation for customer sign-in](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-entra-id-federation-customers)

Keycloak can similarly broker external IdPs and run a configurable first-login flow when it sees a brokered identity for the first time. [Keycloak identity brokering and first login](https://www.keycloak.org/docs/latest/server_admin/#_first_login_flow)

This can improve the sign-in experience for contracted enterprises, but provider setup, discovery buttons, `domain_hint`, and home-realm routing remain deployment configuration. They should not change the application's organization or membership semantics. Microsoft notes that automatic email-domain routing for new users has limited support and documents explicit provider buttons or `domain_hint` for specific configurations. [Entra federation FAQ](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-entra-id-federation-customers#frequently-asked-questions)

## One portable application contract

The product needs only the following normalized authentication result:

```text
ExternalIdentity
  Issuer            required, exact validated issuer
  Subject           required, exact validated OIDC subject
  DisplayName       optional presentation data
  Email             optional mutable contact/hint
  EmailAssurance    provider-normalized: assured | unassured | absent
```

`(Issuer, Subject)` is the unique durable identity key. OIDC standard claims such as `email` and `email_verified` are optional; the specification defines their meaning but does not require every provider to emit them. [OIDC standard claims](https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims)

Microsoft likewise states that `email`, UPN, and `preferred_username` are mutable and must not identify or authorize a user; it recommends stable `sub`, or Entra-specific `oid` with tenant context. For portability, retain `(iss, sub)` as the application's baseline identity key. [Microsoft ID-token claims reference](https://learn.microsoft.com/en-us/entra/identity-platform/id-token-claims-reference#use-claims-to-reliably-identify-a-user)

The application should not normalize Entra `oid`/`tid`, Keycloak internal IDs, B2B guest status, upstream home-tenant IDs, groups, or IdP roles into product authorization in v1. A deployment may record provider diagnostics separately, but product organization membership continues to reference the product `User`.

### Multiple product organizations

One product `User` can have memberships in many product organizations because membership is keyed independently from `ExternalIdentity`. Signing in once resolves the user; the route-selected organization then chooses one of that user's active memberships. Neither Keycloak realms/organizations nor Entra tenants/groups need to mirror every product organization.

The inverse is also important: the same human authenticating through a different issuer or a pairwise subject is not automatically the same product `User`. Automatic email-based linking is unsafe because the email is mutable; a deliberate account-linking/recovery flow can be added only when a real provider-switch scenario exists. [OIDC subject identifier types](https://openid.net/specs/openid-connect-core-1_0.html#SubjectIDTypes) [Microsoft claims validation guidance](https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation)

## Clarifying invitation-email matching

The earlier question “require a verified provider email match” asks what prevents an invitation sent to `alice@example.com` from being forwarded and accepted while signed in as `bob@example.net`.

There are two independent proofs:

1. possession of the high-entropy, expiring invitation link proves access to whoever received the message, subject to forwarding or mailbox compromise;
2. a validated OIDC session proves control of `(issuer, subject)`, but does not portably prove a particular email address because `email` and `email_verified` are optional claims. [OIDC standard claims](https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims)

Therefore, a hard dependency on `email_verified=true` is not fully provider-neutral. Microsoft explicitly says an app requesting the `email` scope must handle the claim being absent, and workforce-token email values are mutable. [Microsoft OIDC scopes](https://learn.microsoft.com/en-us/entra/identity-platform/scopes-oidc) [Microsoft ID-token claims reference](https://learn.microsoft.com/en-us/entra/identity-platform/id-token-claims-reference)

Recommended v1 policy:

- Invitation acceptance always requires both the unconsumed product invitation and a validated OIDC session.
- When the configured provider supplies a verified email, require its normalized value to match the invitation address. Keycloak can satisfy this with realm email verification; External ID federation supports explicit `email` and `email_verified` claim mapping. [Keycloak registration documentation](https://www.keycloak.org/docs/latest/server_admin/#_registration) [External ID custom OIDC claims mapping](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-custom-oidc-federation-customers)
- If a directory-gated provider cannot supply a verified address, do not infer verification from `email`, UPN, or `preferred_username`. Either declare the invitation link plus authenticated directory admission sufficient for that deployment, or add a product-owned second verification message to the invited mailbox. Make this an explicit deployment policy and audit which path accepted the invitation.
- Never use the matched email as the stored identity key or as continuing authorization data.

This preserves provider neutrality while allowing a higher-assurance deployment. The template should prove the verified-provider-email path locally with Keycloak. It need not implement product-owned second verification until an actual Entra workforce pilot demonstrates that its accepted claims cannot meet the assurance contract.

## Minimal v1 recommendation

1. Implement the ordinary OIDC authorization-code flow and one `(iss, sub)` claim normalizer.
2. Implement application-owned invitation, invitation-email outbox, JIT `User`/`ExternalIdentity`, and atomic membership/role assignment.
3. Run Keycloak locally in open-registration mode with verified email.
4. Document two deployment profiles, not two application flows:
   - `OpenRegistration`: the IdP offers sign-up; a new invitee can create an IdP account.
   - `DirectoryGated`: the IdP admits only pre-provisioned/federated/assigned identities; directory admission is an operator prerequisite.
5. For a new customer-facing Azure pilot, evaluate an External ID external tenant, not legacy Azure AD B2C. For an internal or tightly contracted ERP deployment, evaluate an Entra workforce enterprise application with assignment required and B2B guests where needed. These have different UX, administration, and pricing and are deployment choices rather than interchangeable labels. [External ID tenant comparison](https://learn.microsoft.com/en-us/entra/external-id/external-identities-overview#compare-external-id-feature-sets) [Enterprise application assignment](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/application-properties#assignment-required)
6. Defer IdP Admin APIs, Graph invitations, SCIM, product account linking, dynamic multi-issuer registration, and home-realm discovery until a named deployment requires them.

### Required behavior tests

The application-level suite should be reusable against each supported OIDC deployment:

- first login JIT-creates one product identity keyed by `(iss, sub)`;
- repeat login remains idempotent;
- one user accepts invitations into two product organizations;
- a wrong verified provider email cannot consume the invitation;
- an absent or unverified provider email follows the configured deployment policy and is audited;
- an IdP rejection leaves the application invitation pending;
- accepting an invitation never creates, deletes, or assigns a provider-directory account;
- changing display name or email does not create another product user;
- the same email under a different `(iss, sub)` is not auto-linked;
- suspended product membership remains denied even if IdP authentication succeeds.

## Mapping static product roles to OpenFGA later

### Why the proposed v1 model maps cleanly

OpenFGA's own rule of thumb is: if a role is built into the application, define it in the authorization model; if an end user can define it, represent it through tuples and a `role` type. It calls directly assignable relations the preferred representation for static roles. [OpenFGA model design principles](https://openfga.dev/docs/best-practices/modeling-design-principles) [OpenFGA role-modeling guidance](https://openfga.dev/docs/best-practices/modeling-roles)

The proposed v1 concepts translate as follows:

| v1 relational/code concept | Eventual OpenFGA representation | Migration note |
| --- | --- | --- |
| Stable system role code `organization-administrator` | Directly assignable `organization#administrator` relation in the model | The role's existence and semantics remain versioned source, not tenant data. |
| Permission `access.members.manage` granted to that role | Computed `organization#can_manage_members: administrator` relation in the model | Static role-to-permission wiring is model code. |
| User `u123` assigned the role in organization `o456` | Tuple `user:u123 administrator organization:o456` | Use opaque product IDs, not email or IdP subject, in tuples. OpenFGA advises against PII in tuple identifiers. [Tuple/API best practices](https://openfga.dev/docs/getting-started/tuples-api-best-practices) |
| Product user belongs to several organizations | One role/member tuple per organization | OpenFGA's multi-tenant pattern models `organization` explicitly and supports a single store for all tenants. [Multi-tenant SaaS pattern](https://openfga.dev/docs/use-cases/multi-tenant-saas) |
| Module-owned business condition, such as order state or approval amount | Remains an application/domain policy initially | Do not duplicate transactional module state merely to make it graph data. OpenFGA says entity hierarchies and searchable/filterable data commonly remain in the application database. [Source-of-truth guidance](https://openfga.dev/docs/best-practices/source-of-truth) |

Illustrative eventual model fragment, not a v1 artifact:

```fga
model
  schema 1.1

type user

type organization
  relations
    define member: [user]
    define administrator: [user]
    define can_manage_members: administrator
    define can_manage_access: administrator
```

This shape also preserves the agreed separation: Organization Administrator governs access management and is not automatically a business-module superuser. Sales, Inventory, and Purchasing can define separate relations/permissions when there is an OpenFGA-worthy resource relationship.

### Where the model lives and how it is versioned

OpenFGA supports modular DSL source controlled in multiple `.fga` files plus an `fga.mod` manifest containing the schema version and file list. The CLI combines and writes those files as one authorization model. This can align ownership with product modules while still producing one coherent deployed model. [OpenFGA modular models](https://openfga.dev/docs/modeling/modular-models)

Writing a model creates a new immutable authorization-model ID. OpenFGA strongly recommends pinning that ID with the store ID in application configuration; omitting it selects the most recently created model. [Immutable authorization models](https://openfga.dev/docs/getting-started/immutable-models) [FGA CLI model versions](https://openfga.dev/docs/getting-started/cli#work-with-authorization-model-versions)

Consequently, “roles defined in code” should mean reviewed model/source files and tests in the repository, deployed by an explicit migration/release step. It should not mean that every application replica rewrites the OpenFGA model on startup.

### Tuples and source of truth

OpenFGA relationship tuples hold `user`, `relation`, and `object`; they are the instance data that assign a product user to a role on a particular organization/resource. OpenFGA notes that role membership may reasonably live in OpenFGA, but it does not store role display metadata, so an application still needs relational metadata if roles are visible/editable. [OpenFGA source-of-truth guidance](https://openfga.dev/docs/best-practices/source-of-truth)

For this project, the safest later adoption is staged:

1. keep Access's relational membership/role assignments authoritative;
2. project assignments into OpenFGA through an outbox-backed, replayable projection;
3. shadow OpenFGA checks against existing authorization tests;
4. cut reads over only after reconciliation and failure behavior are proven;
5. decide explicitly whether OpenFGA then becomes authoritative for fine-grained grants.

OpenFGA tuple writes are calls to its separate relationship-tuple API; they do not participate in an EF Core database transaction. [OpenFGA relationship-tuple writes](https://openfga.dev/docs/getting-started/update-tuples) A projection approach therefore requires lag metrics, replay/bootstrap, deletion handling, reconciliation, and a defined default-deny/fallback policy.

### Conditions and request context are not a reason to move business rules

OpenFGA conditions can evaluate typed context using CEL for policies such as time windows, network ranges, quotas, or resource attributes; persisted tuple context and request context can participate in a check. [OpenFGA conditions](https://openfga.dev/docs/modeling/conditions)

Contextual tuples are ephemeral relationships supplied with a query instead of persisted. OpenFGA documents them for active-organization context and token-derived group membership, but also warns that token-derived access persists until token expiry and that contextual data complicates change feeds/search projections. [Contextual tuples](https://openfga.dev/docs/interacting/contextual-tuples) [Organization-context authorization](https://openfga.dev/docs/modeling/organization-context-authorization)

Those features could later gate a graph relationship by the route-selected organization. They should not absorb order-state invariants, approval arithmetic, idempotency, or workflow trust. Those values are already authoritative inside module transactions and remain module-owned policies.

### Migration implications

OpenFGA models are immutable. Adding, removing, or renaming a type/relation writes a new model ID; a rename can require application changes plus tuple copying before the new ID is selected. OpenFGA compares these changes to relational schema migrations and documents a staged rollout. [Model migration guidance](https://openfga.dev/docs/modeling/migrating/migrating-models) [Immutable-model migration example](https://openfga.dev/docs/getting-started/immutable-models#potential-use-cases)

Therefore:

- choose stable role and permission identifiers now, but do not promise they can never change;
- keep behavioral authorization tests independent of storage implementation;
- do not expose tuples or OpenFGA model terms through module contracts in v1;
- do not build a generic `IAuthorizationProvider` solely for hypothetical replacement;
- if OpenFGA is adopted, version model source and tuple migrations like database migrations, pin the deployed model ID, and test old/new application compatibility during rollout.

## Decision-oriented result

### Recommended for v1

- Product-neutral OIDC authentication keyed by `(iss, sub)`.
- Application-owned organizations, invitations, memberships, seeded system roles, permission checks, and audit records.
- One JIT callback path for both open-registration and directory-gated deployments.
- Keycloak open registration with verified email as the local/reference deployment.
- Verified-provider-email matching when the provider can satisfy it; explicit deployment policy when it cannot.
- Static product roles and permissions defined as reviewed code/catalog data, with assignments in Access persistence.

### Deployment options, not product semantics

- Keycloak open or closed registration.
- Entra External ID external tenant for customer/business-customer self-service CIAM.
- Entra workforce tenant with assignment required, optionally using B2B guests, for directory-admin-controlled ERP access.
- Enterprise federation behind Keycloak or an External ID external tenant when a named customer requires it.

### Deferred until triggered

- Keycloak Admin REST and Microsoft Graph provisioning/invitation APIs.
- SCIM lifecycle provisioning.
- Home-realm/domain discovery owned by the product.
- Automatic cross-issuer account linking.
- Tenant-defined roles.
- OpenFGA runtime, model, tuples, projection/reconciliation, and operational footprint.

### Principal risks to prove

- Supported production IdPs may not emit a verified email uniformly; invitation binding needs an explicit tested policy rather than implicit claim guesses.
- Switching issuer/client configuration can change `sub`, especially with pairwise subjects; account migration/linking is a deliberate data migration, not an email join. [OIDC subject identifier types](https://openid.net/specs/openid-connect-core-1_0.html#SubjectIDTypes)
- An Entra workforce deployment and an External ID external-tenant deployment have materially different admission and UX behavior even though both present OIDC to the application.
- A future OpenFGA projection introduces eventual consistency and a second authorization datastore; static role naming alone does not make that migration operationally seamless.
