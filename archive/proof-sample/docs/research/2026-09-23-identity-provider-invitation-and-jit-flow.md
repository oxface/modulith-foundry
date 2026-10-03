# Identity-provider invitation and JIT user flow

Status: Research input for architecture review; not an implementation decision record.

As of: 2026-09-23

## Executive conclusion

Keep organization invitations in the product and let the identity provider do only authentication and account self-service. An organization administrator creates an application `Invitation`; the application emails its own single-use link; the recipient signs up or signs in through the configured OIDC provider; the Access module JIT-creates the application `User` and `ExternalIdentity` from the validated `(issuer, subject)` pair; and the invitation is consumed atomically to create the organization `Membership` and role assignments.

Do not make v1 call the Keycloak Admin REST API or Microsoft Graph to provision ordinary product users. Both Keycloak 26.7 and Entra External ID external tenants already support self-service account creation, while application-owned invitations work unchanged across both providers. IdP administration adds powerful credentials, provider-specific failure modes, duplicate-account races, and password/onboarding behavior without removing the need for application invitation state.

Use the ordinary OIDC authorization-code flow. `login_hint` may prefill or suggest the invited email but is not proof of identity and is explicitly discretionary for the provider. The OIDC middleware owns `state`, nonce, correlation cookies, and callback validation; product invitation state remains a separate server-side/application token concern. The later [OIDC onboarding modes note](./2026-09-23-oidc-onboarding-modes-and-openfga-mapping.md) refines the email-assurance conclusion: `email` and `email_verified` are optional in OIDC, so a directory-gated deployment without an assured address needs an explicit acceptance or application re-verification policy.

No new package is justified by this design.

## Provider capability matrix

| Concern | Keycloak 26.7.4 | Entra External ID external tenant | Portable v1 decision |
| --- | --- | --- | --- |
| Self-service account creation | Realm self-registration adds a registration link to the login page. With `Verify email`, Keycloak recommends verifying the address before setting credentials. Identity-broker first login can also create the local Keycloak user. | A sign-up/sign-in user flow creates the directory user during self-service sign-up. It supports email/password, email one-time passcode, and configured federated providers. | Enable provider-managed self-service sign-up; do not provision a provider account when the app invitation is created. |
| Email assurance | Enable realm `Verify email`; the resulting OIDC identity can expose the standard `email`/`email_verified` claims. | Local email/password sign-up verifies the address using a one-time passcode; email-OTP sign-in proves mailbox access each time. Federated OIDC sign-up requires `email_verified=true` when an email is present. | Require an invited email and configure a provider flow with verified email. Treat email as mutable contact data, never as the durable identity key. |
| JIT product identity | The first successful OIDC callback supplies a stable issuer/subject; a Keycloak realm may create its own brokered user at its first-login flow. | The external-tenant user flow creates the customer directory user and returns a token; a federated Microsoft Entra identity can self-register and be created automatically. | On every validated callback, resolve or create `ExternalIdentity(issuer, subject)` and its product `User`. |
| Admin provisioning | Admin REST can create a user and send an `execute-actions-email` link for actions such as `VERIFY_EMAIL` and `UPDATE_PASSWORD`. | Microsoft Graph can create external-tenant customer users, primarily useful for migration or automated provisioning. The Graph invitation API creates B2B guest users; Microsoft says the external-tenant “invite external user” feature is for tenant administrators and is not compatible with customer CIAM flows. | No IdP admin integration for normal organization invitations. |

Primary sources:

- [Keycloak 26.7.4: self-registration, verify-email, identity brokering, and Admin API alternatives](https://www.keycloak.org/docs/latest/server_admin/#_registration)
- [Keycloak 26.7.4 Admin REST API: create user and execute-actions-email](https://www.keycloak.org/docs-api/26.7.4/rest-api/index.html#_users)
- [Keycloak 26.7.4 `UserResource.executeActionsEmail`](https://www.keycloak.org/docs-api/latest/javadocs/org/keycloak/admin/client/resource/UserResource.html)
- [Entra External ID: create a sign-up/sign-in user flow](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-user-flow-sign-up-sign-in-customers)
- [Entra External ID: identity providers and verified local-email sign-up](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-authentication-methods-customers)
- [Entra External ID: federated Entra users self-register automatically; Graph creation is an alternative](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-entra-id-federation-customers)
- [Entra External ID: ordinary customer accounts are normally created by self-service sign-up](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-manage-customer-accounts)
- [Entra External ID: guest invitation is administrative and incompatible with CIAM user flows](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-manage-admin-accounts#invite-an-admin-guest-account)
- [Microsoft Graph invitation API: creates a B2B collaboration guest](https://learn.microsoft.com/en-us/graph/api/invitation-post?view=graph-rest-1.0)

## Recommended portable flow

### 1. Create the product invitation

An active Organization Administrator submits an invited email and seeded roles. Access writes an invitation with:

- organization ID;
- normalized email plus the original display form;
- role assignments;
- expiry, creator, creation time, and lifecycle state;
- a random high-entropy token digest, never the plaintext token; and
- audit and outbox rows in the same database transaction.

The outbox sends a product-branded link such as `/invitations/accept?token=...`. Resending rotates the token and invalidates the old one. The link is a bearer secret: do not put it in logs, analytics, referrers, or OIDC parameters; apply expiry, one-use consumption, revocation, rate limiting, and generic error responses.

### 2. Start authentication without provisioning

The accept endpoint checks that the digest identifies a pending, unexpired invitation. It does not consume it yet.

If unauthenticated, store only a random, short-lived pending-acceptance handle in protected same-site browser state or server-side state, then initiate the normal OIDC challenge. The framework must generate and validate OIDC `state`, nonce, PKCE, and correlation data. OIDC defines `state` as an opaque request/callback binding normally used for CSRF protection; application code should not replace it with the raw invitation token. [`login_hint` is optional and the provider may ignore it](https://openid.net/specs/openid-connect-core-1_0.html#AuthRequest), so it is only a convenience. [`prompt=login` forces reauthentication and `prompt=select_account` asks for account selection](https://openid.net/specs/openid-connect-core-1_0.html#AuthRequest); use them only for an explicit “use another account” action, not for every invitation.

Keycloak supports ordinary OIDC login and self-registration; clients should not bypass the OIDC flow by linking directly to internal login-action endpoints. [Keycloak documents its browser OIDC flow and warns against direct internal login redirects](https://www.keycloak.org/docs/latest/server_admin/#_oidc). Entra External ID uses the same browser-delegated sign-up/sign-in user flow; it does not require Graph creation first.

### 3. JIT-create the application identity

After the middleware validates the callback and ID token:

1. Canonicalize the validated issuer and subject.
2. Resolve `ExternalIdentity` using the unique key `(Issuer, Subject)`.
3. If absent, create a product `User` plus `ExternalIdentity`; if present, update only explicitly permitted profile/contact fields.
4. Never match or auto-link an existing product user solely by email. Microsoft explicitly documents email as mutable and unsuitable as a durable user identifier; use `sub`/`oid` with the issuer/tenant context instead. [Microsoft ID-token claims reference](https://learn.microsoft.com/en-us/entra/identity-platform/id-token-claims-reference#payload-claims)

Provider switching therefore creates another external-identity link only through a deliberate account-linking flow, which is deferred until a real scenario requires it.

### 4. Bind the invitation and membership

Resume through the short-lived pending acceptance and revalidate the invitation. When the provider supplies a verified email, require it to normalize to the invited email. The invitation link proves possession of the mailbox at send time; the claim comparison also prevents a forwarded link from being accepted under an unrelated signed-in account.

- For local Keycloak accounts, require realm email verification and `email_verified=true`.
- For Entra local accounts, the configured sign-up flow verifies email by one-time passcode. Ensure the application token emits the configured email attribute.
- For an Entra custom OIDC federation, map both `email` and `email_verified`; current External ID account creation requires `email_verified=true` when email is present. [External ID OIDC claims mapping](https://learn.microsoft.com/en-us/entra/external-id/customers/reference-oidc-claims-mapping-customers)

If a provider configuration cannot supply an assured matching address, do not infer assurance from mutable email-like claims. The deployment must explicitly choose either invitation-link plus directory admission as sufficient or an application-owned re-verification email; audit which path accepted the invitation.

In one Access transaction:

- lock/recheck the pending invitation;
- create or reactivate the appropriate membership according to explicit product rules;
- assign its invited roles;
- mark the invitation accepted by this user/external identity;
- write audit records; and
- write any actual outgoing integration message to the outbox.

Concurrent acceptance must be protected by a uniqueness constraint and optimistic/concurrency checks. Replaying an already-consumed token by the same user should produce an idempotent success page; a different user receives a generic unusable-invitation result.

## Existing-account cases

| Case | Expected result |
| --- | --- |
| Existing IdP and existing product external identity | Authenticate, resolve the same user, and accept idempotently. |
| Existing IdP but first use of this product | JIT-create product `User`/`ExternalIdentity`, then accept. |
| New IdP account | Provider self-service registration creates it; callback then JIT-creates the product identity. |
| Existing active membership in the organization | Do not duplicate it; show success and apply only an explicitly defined invitation role policy. The safest initial policy is to union the invited seeded roles, audit the change, and never remove existing roles. |
| Signed in as a different verified email | Do not consume the invitation. Offer “use another account,” which may issue a fresh OIDC challenge with `prompt=select_account` or `prompt=login`. |
| Same email, different `(issuer, subject)` from an existing product user | Do not auto-link. Require a later deliberate linking/recovery process or operator resolution. |

## Why the application should not provision IdP users in v1

### Keycloak

Keycloak Admin REST can create users and `execute-actions-email` can send time-limited links for required actions. That is useful for a closed workforce realm where self-registration is forbidden. It is not needed for this customer/product invitation flow. It would require a confidential service account with user-management capability, make Access depend on Keycloak availability during invitation creation, and force recovery for partial outcomes such as “IdP user created, Access invitation transaction failed.” It also duplicates Keycloak's supported self-registration and broker-first-login behavior.

### Entra External ID

Microsoft Graph can create a customer user and is appropriate for migration or controlled provisioning. The Graph `/invitations` endpoint instead creates a B2B guest and carries `User.Invite.All`; Microsoft states that external-tenant guest invitation is for administrators and is not compatible with CIAM user flows. Using it for product organization membership would conflate the application's Organization Administrator with an Entra directory administrator and incorrectly move product authorization into the IdP.

### Adoption trigger

Add privileged IdP provisioning only when a named product/environment requires accounts to exist before first login—for example, self-registration is prohibited by a contracted enterprise realm, a bulk migration needs deterministic pre-creation, or a customer requires lifecycle provisioning through SCIM. Treat that as a provider-specific adapter with least-privilege credentials, idempotent reconciliation, and failure recovery; it is not part of the portable invitation contract.

## Minimum proof suite

Run the same product behavior tests against Keycloak locally and Entra External ID before the Azure pilot:

1. New account, verified email, JIT user creation, and invitation acceptance.
2. Existing product user accepts an invitation to a second organization.
3. Existing active membership receives no duplicate membership.
4. Wrong signed-in email cannot consume the invitation and can switch accounts.
5. Expired, revoked, rotated, and replayed tokens behave safely.
6. Two concurrent acceptances create one membership and one accepted invitation.
7. Missing/unverified email follows the explicit deployment policy and records the chosen assurance path.
8. Provider callback succeeds after app restart because pending-acceptance state is durable enough for its short lifetime.
9. Mail/outbox retry does not create duplicate invitations or tokens.
10. No raw invitation token appears in structured logs, telemetry, or OIDC request parameters.
