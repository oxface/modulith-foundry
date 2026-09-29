# Invitation acceptance security review

Date: 2026-09-29

## Question

Does the invitation acceptance flow use current browser/OIDC and ASP.NET Core security mechanisms without creating more bespoke security machinery than the use case justifies?

## Conclusion

Keep the present architecture, but reduce the authentication state it retains. The Redis handoff is justified because it removes the raw invitation bearer from subsequent redirects and supports the common wrong-account recovery path. It should remain a small authentication-adapter detail, not become a workflow framework.

Before checkpointing this increment:

1. Keep `SaveTokens = true`. Standards-based RP-initiated logout uses the ID token as `id_token_hint` for reliable logout and wrong-account recovery. The option also retains access and refresh tokens when the provider returns them, but they remain inside the protected server-side Redis ticket and are never exposed to the browser. This modest extra state is preferable to custom token-selection lifecycle code in the template.
2. Treat the provider-verified email as callback-only evidence. Return it beside the completed product user from OIDC completion, use it for this invitation decision, and do not add it to the durable cookie principal.
3. Remove the invitation acceptance handle from `AuthenticationProperties.Items` as soon as the OIDC callback captures it, before the properties become part of the cookie ticket.
4. Evaluate `SameSite=Strict` for the BFF session cookie with the topology suite. The newly published browser-application BCP recommends it. The invitation entry point and OIDC callback do not require the existing product session cookie, so this flow should tolerate Strict; retain Lax only if a concrete same-product navigation requirement fails.
5. On wrong-account retry, use OIDC RP-initiated logout and then issue a fresh challenge. `prompt=login` provides reauthentication but does not necessarily provide account switching, and Keycloak does not currently implement `prompt=select_account`.

Do not add a saga, JWT invitation, custom OAuth state implementation, downstream-token machinery, DPoP, private-key client authentication, or continuous IdP introspection to this slice.

## Standards comparison

### Browser authentication

The current flow uses an ASP.NET Core confidential OIDC client, authorization code response type, PKCE, provider metadata, issuer/audience/signature/lifetime validation through the framework, nonce/correlation/state handling, and `form_post`. This matches the OAuth Security BCP and ASP.NET Core guidance. `form_post` also avoids putting the authorization code in browser history. ASP.NET Core 10 uses Pushed Authorization Requests by default when provider discovery advertises support; the template should keep the native `UseIfAvailable` behavior rather than adding provider-specific PAR code.

Sources:

- [RFC 9700: OAuth 2.0 Security Best Current Practice](https://datatracker.ietf.org/doc/html/rfc9700#section-2.1)
- [RFC 10017: OAuth 2.0 for Browser-Based Applications — BFF](https://datatracker.ietf.org/doc/html/rfc10017#section-6.1)
- [ASP.NET Core 10 OIDC web authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0)
- [ASP.NET Core `PushedAuthorizationBehavior`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.openidconnect.pushedauthorizationbehavior?view=aspnetcore-10.0)

### BFF session and CSRF

RFC 10017 presents the BFF as the strongest of its three browser architecture patterns, while explicitly acknowledging its operational complexity. It requires Secure and HttpOnly cookies, recommends host-only/path-root/Strict cookies, and requires a proper CSRF defense. The current opaque cookie plus Redis `ITicketStore`, Secure/HttpOnly/`__Host-` attributes, explicit antiforgery header on state-changing endpoints, and server-side logout fit that model.

The current `SameSite=Lax` session cookie is a conscious compatibility choice rather than a broken control because write endpoints still require an antiforgery token. Nevertheless, Strict is now the preferred BFF default and should be tried. The antiforgery cookie is already Strict.

The custom endpoint filter remains appropriate on .NET 10 JSON Minimal APIs: framework antiforgery is not automatically enforced merely because a JSON endpoint mutates state. Avoid replacing the clear endpoint marker with a home-grown global heuristic.

Sources:

- [RFC 10017 BFF cookie and CSRF requirements](https://datatracker.ietf.org/doc/html/rfc10017#section-6.1.3)
- [ASP.NET Core 10 antiforgery guidance](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [ASP.NET Core SameSite guidance](https://learn.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-10.0)

### Invitation bearer

No dedicated invitation protocol is standardized. The closest owned guidance is OWASP's emailed reset-token model: use a cryptographically random, sufficiently long, expiring, single-use, securely stored URL token; use a configured trusted public base URL and HTTPS; suppress referrer leakage; and rate-limit attempts.

The implementation satisfies the data-model requirements:

- 256 random bits for the invitation secret;
- only a SHA-256 digest stored on `Invitation`, compared in fixed time;
- explicit expiry and consumed status;
- resend rotates the secret/generation and supersedes the old delivery;
- public base URL comes from validated configuration rather than the request Host;
- acceptance requires both the bearer and a freshly validated OIDC identity with matching provider-verified email;
- the raw secret is cleared from the durable delivery row after confirmed send or supersession.

The invitation row is correctly retained after acceptance. It is business/audit/idempotency history, not disposable secret state. The digest is non-reversible and is still useful during concurrent/repeated completion.

Source: [OWASP Forgot Password Cheat Sheet — URL tokens](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html#url-tokens)

### Email binding

OIDC defines `email_verified=true` to mean that the provider took affirmative steps to verify control of that address at the time of verification; it explicitly leaves the method and trust framework provider-specific. Email is not a stable unique identity key. The application therefore correctly keys users by `(iss, sub)` and uses the verified email only as evidence for this invitation decision.

The email link already demonstrates current mailbox access, while the provider email match prevents a forwarded link from being accepted by a differently identified account. Keeping both checks is a defensible product policy, but it is not universal OIDC behavior. A production IdP that cannot issue an acceptable verified-email claim needs an explicit alternative policy; the code must not infer assurance from UPN, `preferred_username`, or an unverified email.

Source: [OpenID Connect Core 1.0 standard claims](https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims)

## Why retain the Redis pending-acceptance handoff

A simpler implementation could place the protected invitation ID and secret directly in OIDC `AuthenticationProperties`, then tell a mismatched user to sign out and reopen the email. ASP.NET Core would protect the state, so this is not inherently invalid.

The current Redis handoff earns its modest extra code because it provides two concrete behaviors:

1. After the first inbound request, the raw bearer does not travel through the authorization request, callback, result URL, or retry URL.
2. A user already signed into the wrong provider account can explicitly switch accounts without finding and reopening the email.

Redis is already required for BFF tickets. The pending record contains only invitation ID and secret, is Data-Protection-protected, addressed by a random 128-bit handle, and expires after 15 minutes. It needs no general abstraction, database model, cleanup worker, distributed lock, or event. Redis loss merely requires reopening the email; PostgreSQL remains authoritative.

This is the boundary at which the feature remains proportionate. If the retry behavior is later removed, the Redis handoff should be reconsidered rather than kept by inertia.

## State inventory and cleanup

| State | Required lifetime | Cleanup |
| --- | --- | --- |
| Invitation row, role assignments, recipient, digest, status | Product retention period | Retain after acceptance; later product retention policy may archive/delete |
| Invitation email delivery metadata | Operational retention period | Retain delivery facts; clear protected payload after send or supersession |
| Protected email payload containing raw secret | Until confirmed send/supersession | Set to null transactionally |
| Pending Redis acceptance | OIDC/retry window, currently 15 minutes | Delete on success or terminal failure; preserve mismatch for retry; TTL handles abandonment |
| OIDC state, nonce, PKCE, correlation cookie | One remote-authentication transaction | ASP.NET Core creates, validates, and consumes it |
| Acceptance handle in `AuthenticationProperties` | Until callback dispatch | Application must remove before cookie sign-in |
| Provider-verified email evidence | Current validated callback only | Do not persist as a product claim or ticket property |
| Provider tokens | Product session | Retain in the protected server-side ticket through native `SaveTokens`; the ID token supports RP-initiated logout, and all tokens are deleted with the ticket on logout/expiry |
| BFF authentication ticket | Product session | Redis expiry/revocation/logout; browser holds only opaque cookie key |

## Explicitly accepted gaps

### First-hop bearer exposure

An emailed URL bearer necessarily appears in the email client and initial browser navigation. It may enter browser history and can be observed by the first reverse proxy/gateway. Application OTel redaction cannot sanitize an upstream component.

Mitigations retained now:

- HTTPS-only public URL in deployed environments;
- `Cache-Control: no-store` and `Referrer-Policy: no-referrer`;
- immediate replacement with an opaque Redis handle;
- no third-party content on the result surface;
- high entropy, expiry, single consumption, and matching authenticated identity;
- application traces redact query values by default and a topology test proves the raw bearer is absent from exported application logs/traces.

Deployment requirement: ingress, gateway, WAF, CDN, and access-log configurations must suppress or redact query strings for this route. OpenTelemetry's ASP.NET Core option only replaces values in the `url.query` trace attribute; it does not sanitize custom logs or infrastructure logs.

Sources:

- [RFC 9700 credential leakage via referrer and browser history](https://datatracker.ietf.org/doc/html/rfc9700#section-4.2)
- [OpenTelemetry ASP.NET Core query redaction](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md#http-server-semantic-conventions)

### Abuse and cost protection

The anonymous acceptance entry point can create short-lived Redis records and OIDC redirects for syntactically valid garbage. The 15-minute TTL bounds duration, not request rate. Querying PostgreSQL before creating the Redis record would make an attack more expensive for the application and may add an invitation-enumeration signal; it is not the preferred fix.

Before internet exposure:

- apply an edge/gateway rate and concurrency limit to the acceptance route;
- suppress query logging there;
- add a conservative in-process ASP.NET Core endpoint limiter as defense in depth if justified by the selected gateway;
- configure trusted forwarded-header sources before using client IP partitions;
- use no request queue for abusive anonymous traffic;
- load-test the chosen thresholds.

Do not claim the in-process limiter is a global distributed or DDoS control. The edge limit is the cost boundary; the application limit protects each replica.

Sources:

- [ASP.NET Core 10 rate limiting middleware](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [ASP.NET Core proxy and trusted forwarded headers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0)

### Session and IdP revocation

The Redis ticket currently has an eight-hour sliding application session. Product membership and authorization changes are evaluated from product state, but disabling the account at the IdP does not automatically invalidate an already-issued product session. This is a normal federated-session trade-off, not something invitation acceptance should solve.

Invitation retry deliberately performs OIDC RP-initiated logout before requesting fresh authentication. Reauthentication through `prompt=login` is not equivalent to selecting another account, and support for `prompt=select_account` is provider-dependent. Native token persistence supplies the ID-token logout hint without custom authentication lifecycle code; the protected Redis ticket remains the only copy held by the application.

Sources:

- [OpenID Connect RP-Initiated Logout 1.0](https://openid.net/specs/openid-connect-rpinitiated-1_0.html#RPLogout)
- [Keycloak account-selection support request](https://github.com/keycloak/keycloak/issues/42064)

Before production, choose and document idle and absolute session limits for the actual product risk. Back-channel logout, provider event integration, periodic reauthentication, and high-risk-operation step-up remain triggered capabilities, not v1 template machinery. Saved access and refresh tokens are not consumed by current product code and must not become an implicit downstream-token API without a separate decision.

Source: [OWASP Session Management Cheat Sheet — expiration](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html#session-expiration)

### Data Protection availability

Local single-host development may use the user-profile key ring. Multi-replica/container deployment must share a durable key ring with a stable application name. Azure Blob Storage plus Key Vault protection and managed identity remains the selected Azure design. Losing the key ring invalidates tickets and makes pending protected email/acceptance payloads unreadable; this must fail closed and alert.

Source: [ASP.NET Core 10 Data Protection key storage providers](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0)

## Complexity explicitly rejected

- JWT/self-contained invitation tokens: add key/claim/revocation/versioning hazards without removing the need for server-side invitation state.
- Storing the raw bearer on the Invitation: unnecessary breach amplification.
- A product-owned second email verification flow before a real provider requires it.
- Generic pending-operation, workflow, saga, or distributed-lock abstractions for one 15-minute OIDC handoff.
- Introspection or UserInfo calls on every request.
- DPoP or sender-constrained access tokens when the application does not expose provider tokens to the browser or call downstream resource servers.
- `private_key_jwt` in the provider-neutral local template; a production deployment may select it when its IdP and secret-management posture justify the operational cost.
- A database lookup on every anonymous link open merely to avoid writing a small expiring Redis entry.

## Resulting minimal design

The smallest defensible implementation is therefore:

1. Persist an expiring, single-use invitation with a digest of a random secret.
2. Deliver the raw secret once through a protected durable email-delivery record.
3. On first link open, replace the raw bearer with a short-lived protected Redis record and opaque handle.
4. Use the framework OIDC code/PKCE/nonce/state/PAR behavior.
5. JIT-link `(iss, sub)` and derive verified email only for this callback.
6. Atomically validate secret, expiry, status, verified-email match, membership creation, roles, and audit.
7. Delete ephemeral state, retain business history, and redirect to the organization route.
8. Make infrastructure query-log suppression, shared Data Protection keys, session policy, and edge rate limiting deployment gates rather than pretending application code solves them.
