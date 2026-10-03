# Invitation email delivery dependency check

Status: Accepted implementation evidence.

Last reviewed: 2026-09-26.

## Decision

Use the following narrow stack for Access-owned invitation email:

| Dependency | Selected version | License | Purpose |
| --- | --- | --- | --- |
| MailKit | 4.18.0 | MIT | SMTP adapter in the Access implementation project |
| Mailpit | v1.31.1 | MIT | Local-only SMTP sink, UI, and topology-test API |
| Microsoft.AspNetCore.DataProtection.Abstractions | 10.0.12 | MIT | Protect the recoverable invitation delivery payload at rest |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.12 | MIT | Native `BackgroundService` lifecycle for the Access-owned dispatcher |

No Mailpit-specific Aspire package, provider SDK, template engine, or general notification framework is justified by this use case. The AppHost uses the official pinned Mailpit container and passes its allocated SMTP endpoint to the API. `IEmailTransport` is the provider-neutral replacement seam; the default adapter uses SMTP.

## Reliability boundary

Invitation state, selected roles, audit, and one protected email-delivery row commit in the Access transaction. The bearer secret is generated with 256 bits of cryptographic randomness; only its SHA-256 digest is stored on the Invitation. The worker needs the raw secret after a process crash, so the self-contained email payload is protected with ASP.NET Core Data Protection before it is stored.

A database lease makes multiple application replicas safe to run. SMTP remains an inherently ambiguous external effect: a process can fail after the server accepts a message but before `sent_at` commits. A retry deliberately sends the same invitation generation and link. Explicit resend is different: it rotates the secret, increments the generation, extends expiry, and supersedes any unsent older delivery.

Data Protection keys are therefore application data, not disposable machine state. Every production replica must share a durable key ring and application name. Azure should persist the ring in Blob Storage and protect keys with Key Vault through the official providers. A container volume or another documented provider is required elsewhere. Losing the ring makes pending protected deliveries unreadable; it must alert rather than silently discard or regenerate invitation secrets.

## Primary evidence

- [MailKit 4.18.0 package metadata](https://www.nuget.org/packages/MailKit/4.18.0) and [release notes](https://github.com/jstedfast/MailKit/blob/master/ReleaseNotes.md) identify the current stable package and security history.
- [MailKit license](https://github.com/jstedfast/MailKit/blob/master/LICENSE) is MIT.
- [Mailpit releases](https://github.com/axllent/mailpit/releases) identify v1.31.1 as the current release; it includes hardening after the earlier 2026 advisories.
- [Mailpit Docker documentation](https://mailpit.axllent.org/docs/install/docker/) documents the official `axllent/mailpit` image and default HTTP 8025 / SMTP 1025 ports.
- [Mailpit health endpoints](https://mailpit.axllent.org/docs/integration/healthcheck/) document `/livez` and `/readyz`; the AppHost uses readiness.
- [Mailpit SMTP and API documentation](https://mailpit.axllent.org/docs/usage/sending-messages/) documents SMTP behavior, while the [v1 API](https://mailpit.axllent.org/docs/api-v1/) supports topology verification.
- [ASP.NET Core Data Protection key storage providers](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0) documents file-system, Azure Blob Storage, Redis, and EF providers, including the multi-instance Azure guidance.
- [ASP.NET Core key management](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-management?view=aspnetcore-10.0) documents key rotation, expiry, caching, and multi-instance behavior.
- [Data Protection abstractions 10.0.12](https://www.nuget.org/packages/Microsoft.AspNetCore.DataProtection.Abstractions/10.0.12) and [Hosting abstractions 10.0.12](https://www.nuget.org/packages/Microsoft.Extensions.Hosting.Abstractions/10.0.12) are Microsoft-owned MIT packages aligned with the repository's .NET 10 servicing baseline.

## Deferred deliberately

- Invitation acceptance, verified-provider-email matching, and JIT membership creation are the next increment.
- HTML branding remains one simple text/HTML renderer until another concrete email proves a reusable template system is needed.
- Delivery retention and purge jobs wait until operational retention requirements exist.
- Azure Blob/Key Vault packages and Azurite wiring wait for the Azure compatibility increment; the production requirement is documented now and may not be waived.
- Generic inbox/outbox EF helpers wait for a second real module implementation and a deletion-based extraction review.
