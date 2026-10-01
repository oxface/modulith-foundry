# Temporary pattern-focused review

The template's goal is to prove modular-monolith capabilities, then extract justified reusable libraries and retain a working sample. Repeated sample-domain plumbing should not consume the same owner-review attention as new technical approaches. Domain correctness still matters to the demonstrated guarantees, and all changes remain subject to testing and repository conventions.

This workflow applies during the remaining planned implementation slices. Stop using automatic staging when library extraction and sample creation begin; agree on the next review workflow then.

## Classification and staging

At the end of each increment, inspect every changed and new file:

1. Match routine reuse to an owner-approved registry entry and its reference implementation. Filename suffixes alone are not evidence. Reuse must preserve the entry's design, responsibility, testing seam, and guarantees.
2. Automatically stage files containing only routine reuse. This includes new files and routine additions to existing files, such as named DbSets, mappings, registrations and audit action identifiers.
3. Leave files introducing, extending or adjusting a pattern unstaged. A file containing both routine and review-worthy changes stays wholly unstaged; do not partially stage it.
4. Leave uncertain changes and unrelated owner changes unstaged. Explain uncertainty rather than treating repetition as approval.
5. Verify the complete change set, staged and unstaged. Staging changes review focus, not verification requirements or the exact-change-set commit approval gate.

Always require owner review for:

- Architecture, module responsibilities/dependencies, design-pattern changes, external library/tool selection, and shared-library extraction.
- Changes to authorization, tenancy, transactions, concurrency, security or message-delivery guarantees, even when the syntax resembles an approved implementation. Applying an unchanged approved mechanism is routine reuse; changing its guarantee is not.
- New test approaches, fixtures or infrastructure designs: for example switching from direct Contracts/service calls to HTTP, introducing RabbitMQ Testcontainers, or introducing browser E2E testing. New cases or a domain-specific fixture using an approved design and seam may be routine reuse.
- All AI engineering work, including Microsoft Agent Framework and evaluations, even when repeating an implementation.
- Changes to this workflow or registry, and mechanisms explicitly marked provisional below.

## Handoff

Report the coherent outcome and verification, then distinguish:

- **Review needed:** every new/changed pattern, guarantee or other important concern, with links to the files worth reviewing and the reason. Include mixed files here.
- **Routine reuse staged:** a compact summary identifying the reused registry entries and any relevant limitations. These files remain available for owner inspection.

If every diff is routine reuse, a concise verification summary and request for checkpoint approval are sufficient. Commit only after explicit owner approval of the complete exact change set, including staged and unstaged work. Registry approval is not commit approval.

## Registry

**Status: owner-approved on 2026-10-01.** The entries below are eligible for automatic staging within their stated boundaries. Approval is explicit; there is no automatic two-occurrence promotion. Extend entries only through owner review.

| ID | Routine reuse candidate | Reference implementation | Boundary |
| --- | --- | --- | --- |
| HTTP | Thin Minimal API adapters calling module Contracts and explicitly mapping results to responses/Problem Details | [CustomerEndpoints](../../apps/Api/Modules/Sales/Customers/CustomerEndpoints.cs) | Reuse existing organization scope and antiforgery conventions. Changes to authentication, scope, exposure of internal operations or response security require review. |
| MAP | Small explicit entity-to-Contract and Contract-to-response mappings | [CustomerMappings](../../modules/Sales/Sales/Customers/CustomerMappings.cs), [SalesOrderResponses](../../apps/Api/Modules/Sales/Orders/SalesOrderResponses.cs) | No mapping framework, hidden IO, or new sensitive-data exposure. Private single-caller mappings and shared internal mapping classes follow existing conventions. |
| STATE | Simple state-stored aggregates/entities with private construction and explicit validated creation | [Customer](../../modules/Sales/Sales/Customers/Customer.cs) | Sample-local business rules using this design are routine; new consistency boundaries, cross-aggregate policies, persistence lifecycles or event-sourcing mechanics require review. |
| EF | Named internal DbSets and ordinary per-entity EF configuration for module-owned persistence | [SalesDbContext](../../modules/Sales/Sales/Persistence/SalesDbContext.cs), [CustomerConfiguration](../../modules/Sales/Sales/Customers/Persistence/CustomerConfiguration.cs) | Preserve explicit tenant ownership and existing filter semantics. Generated migrations/snapshots follow the classification of their model change; generation is not a blanket exemption. Changes to isolation, concurrency, locking or deletion guarantees require review. |
| CQRS | Operation-specific handlers/results and direct module-local read projections | [CreateCustomerHandler](../../modules/Sales/Sales/Customers/CreateCustomer/CreateCustomerHandler.cs), [CustomerQueries](../../modules/Sales/Sales/Customers/Queries/CustomerQueries.cs), [CreateCustomerResult](../../modules/Sales/Sales.Contracts/Customers/CreateCustomerResult.cs) | No generic bus/repository/pipeline or changed Contracts responsibility. Existing transaction/permission checks may be reused, not redesigned under this entry. |
| AUDIT | New stable audit action/reason identifiers and explicit audit records using existing persistence | [SalesAuditActions](../../modules/Sales/Sales/Audit/SalesAuditActions.cs), [SalesAuditEntry](../../modules/Sales/Sales/Audit/SalesAuditEntry.cs), [CreateCustomerHandler](../../modules/Sales/Sales/Customers/CreateCustomer/CreateCustomerHandler.cs) | Changes to audit atomicity, sensitive payload handling, event-derived audit or automatic dispatch require review. |
| DI | Routine registration of new handlers, queries and capabilities through module composition | [SalesModule](../../modules/Sales/Sales/Composition/SalesModule.cs) | Preserve existing lifetimes and composition responsibilities. New workers, transports, shared seams or changed lifetime behavior require review. |
| PG-CONTRACT | PostgreSQL Testcontainers tests through module-owned Contracts in fresh DI scopes | [CustomerPersistenceTests](../../tests/PersistenceTests/CustomerPersistenceTests.cs), [SalesApprovalFixture](../../tests/PersistenceTests/SalesApprovalFixture.cs) | Additional cases and similarly composed domain fixtures are routine. Preserve real database semantics and the distinction between fault/setup SQL and product assertions. New containers, fixture lifecycle/concurrency strategies, fake boundaries or assertion seams require review. |
| HTTP-TOPOLOGY | Authenticated HTTP scenarios using the existing Aspire topology, OIDC helpers and antiforgery client | [LocalRuntimeTests.SalesCustomers](../../tests/TopologyTests/LocalRuntimeTests.SalesCustomers.cs), [LocalRuntimeTests](../../tests/TopologyTests/LocalRuntimeTests.cs) | Additional scenarios reusing this lane are routine. New topology/lifecycle design, identity simulation, HTTP harness or browser automation requires review. |
| APPLICATION | No-IO tests exercising real domain/application objects through existing xUnit conventions | [StockPositionDecisionTests](../../tests/ApplicationTests/StockPositionDecisionTests.cs) | Approves the test style only, not event-sourcing mechanics. New test frameworks, extensive mocking strategies or alternate testing seams require review. |

## Provisional and future areas

Existing implementation and earlier checkpoint approval do not make these mechanisms automatic-staging patterns:

- **Event sourcing:** stream/envelope storage, stable event registry, evolution, inline/live projections, temporal reads, rebuild coordination and version/concurrency handling remain subject to review through the second aggregate and extraction. References: [StockPositionStore](../../modules/Inventory/Inventory/StockPositions/Persistence/StockPositionStore.cs) and the [event-sourcing plan](../plans/event-sourcing.md). Routine adapters/tests around them can qualify separately; changes to these mechanics cannot.
- **Commit-time authorization:** [OrganizationAuthorizationGuard](../../modules/Access/Access/Organizations/OrganizationAuthorizationGuard.cs) and [ApproveSalesOrderHandler](../../modules/Sales/Sales/Orders/ApproveSalesOrder/ApproveSalesOrderHandler.cs) demonstrate a concrete lock protocol, not a generally approved cross-module transaction framework.
- **Reliable delivery:** invitation-email persistence is not evidence that RabbitMQ/Rebus consumers, inbox/outbox relays, acknowledgements, retries or durable orchestration designs are approved. Review their actual implementations and guarantees as the messaging slices arrive.
- **Extraction and scaffolding:** reusable library boundaries, public APIs, second-implementation comparisons, sample separation and generation choices always need owner review.
