# Rebus Endpoint Topology for the Modular Monolith

Status: Research input for architecture review; not an implementation decision record.

As of: 2026-09-23

## Scope and pinned versions

This note compares a single host-wide Rebus endpoint with module-owned endpoints inside the same application process. It assumes that immediate module calls remain in-process and that each asynchronous consumer commits its inbox, owned state, audit, and outgoing outbox rows in its module transaction.

| Package | Pinned version | License |
| --- | ---: | --- |
| `Rebus` | `8.9.4` | MIT |
| `Rebus.ServiceProvider` | `10.7.2` | MIT |
| `Rebus.RabbitMq` | `10.1.1` | MIT |
| `Rebus.AzureServiceBus` | `10.7.1` | MIT |

Primary package evidence: [`Rebus` 8.9.4](https://www.nuget.org/packages/Rebus/8.9.4), [`Rebus.ServiceProvider` 10.7.2](https://www.nuget.org/packages/Rebus.ServiceProvider/10.7.2), [`Rebus.RabbitMq` 10.1.1](https://www.nuget.org/packages/Rebus.RabbitMq/10.1.1), and [`Rebus.AzureServiceBus` 10.7.1](https://www.nuget.org/packages/Rebus.AzureServiceBus/10.7.1). The Azure transport requires Service Bus Standard because it uses topics.

## Recommendation

Use **one stable logical input queue and one Rebus bus per module that actually consumes asynchronous messages**. Do not create an endpoint for a module merely for symmetry. Host those buses in the same application process, but give each consumer module its own handler container through `AddRebusService`.

For the current fulfilment proof this likely means three endpoints:

```text
modulith-foundry.sales
modulith-foundry.inventory
modulith-foundry.purchasing
```

`Access` gets no broker endpoint until it has a real asynchronous consumer. Give each endpoint its own error queue, for example `modulith-foundry.inventory.error`, and its own explicit worker, parallelism, and transport-prefetch settings.

This is the smallest topology that preserves module ownership, permits two modules to consume the same event independently, and lets an extracted module take over its existing queue. It costs a few additional broker queues, connections/channels, hosted services, and registrations. That is justified by an actual v1 multi-module durable workflow; a generic endpoint framework is not.

## Why not one shared host queue?

A shared queue is operationally smallest, but it turns the entire application into one messaging endpoint rather than preserving module boundaries.

Rebus resolves **all** compatible handlers from the endpoint's service provider. It then invokes the resulting handlers in sequence for one broker delivery. If a later handler fails, the delivery fails and can be retried, so an earlier successful handler can be invoked again; module inboxes make that survivable but do not remove the coupling. Primary sources: [`DependencyInjectionHandlerActivator.GetHandlers`](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/ServiceProvider/DependencyInjectionHandlerActivator.cs) and [`DispatchIncomingMessageStep`](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Pipeline/Receive/DispatchIncomingMessageStep.cs).

With one shared queue:

- every subscribed event produces one broker copy for the host queue, followed by in-process dispatch to every compatible handler;
- one slow or poison handler consumes the shared concurrency budget and retry/error policy;
- all modules share one error queue unless extra routing is invented;
- a module cannot be scaled or stopped independently; and
- extraction requires creating a new queue/subscription, deciding what happens to messages already accumulated in the shared queue, and preventing the old handler from continuing to receive them.

The last issue is material: a broker subscription is not a historical replay mechanism. Creating a new service queue later captures future publications, not the consumer's missing current state. The separate consumer-bootstrap/reconciliation design remains necessary under either topology.

## Why not keyed buses in one shared service provider?

`Rebus.ServiceProvider` supports multiple `AddRebus` calls in one container. Exactly one can be the default `IBus`; secondary instances use `isDefaultBus: false`. A key allows explicit lookup and delayed start through `IBusRegistry`. This is real multi-bus support, but it does **not** scope handler registrations by key. All buses created from that service provider use a `DependencyInjectionHandlerActivator` over the same provider, and the activator resolves all registered compatible `IHandleMessages<T>` implementations. Primary sources: [`AddRebus` and primary/key registration](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/Config/ServiceCollectionExtensions.cs), [`IBusRegistry`](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/ServiceProvider/IBusRegistry.cs), and [`DependencyInjectionHandlerActivator`](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/ServiceProvider/DependencyInjectionHandlerActivator.cs).

Keyed buses are therefore not sufficient isolation when two modules subscribe to the same integration-event type: each endpoint could resolve both modules' handlers. Making message types artificially exclusive per endpoint would undermine ordinary event fan-out.

Use `AddRebusService` instead. Its official API creates an independent hosted service with its own service provider; the project README explicitly calls this out as the extra-separation option for a modular monolith. Each module endpoint registers only its own handlers, DbContext, inbox/outbox worker, and dependencies. The host still owns composition and process lifetime. Primary sources: [`AddRebusService`](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/Config/HostBuilderExtensions.cs), [`IndependentRebusHostedService`](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/ServiceProvider/Internals/IndependentRebusHostedService.cs), and the [official `Rebus.ServiceProvider` README](https://github.com/rebus-org/Rebus.ServiceProvider/tree/10.7.2#starting-one-or-more-rebus-instances-using-one-or-more-separate-container-instances).

The tradeoff is explicit: module messaging registrations must be usable in a second provider, and only genuine host singletons should be forwarded. Do not forward scoped DbContexts from the HTTP provider.

## Commands, events, and broker topology

- **Commands** have one owning module and use `Send` with explicit type-based routing to that module's stable queue. Rebus' `Send` asks the configured router for one destination address. Primary source: [`RebusBus.Send`](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Bus/RebusBus.cs).
- **Integration events** use `Publish`. Every consuming module endpoint explicitly subscribes in `onCreated`; that callback runs after the bus is operational but before it starts consuming. Do not publish commands or subscribe to commands. Primary sources: [`RebusBus.Publish` and `Subscribe`](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Bus/RebusBus.cs) and [`AddRebus` lifecycle contract](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/Config/ServiceCollectionExtensions.cs).
- **Immediate module queries and commands** continue through module Contracts in-process. They never route through Rebus merely to resemble a future service.

RabbitMQ uses a direct exchange for point-to-point sends and a topic exchange for publish/subscribe. A subscription binds the endpoint queue to the topic exchange. Primary sources: [`RabbitMqOptionsBuilder` topology](https://github.com/rebus-org/Rebus.RabbitMq/blob/10.1.1/Rebus.RabbitMq/Config/RabbitMqOptionsBuilder.cs), [`RabbitMqTransport.RegisterSubscriber`](https://github.com/rebus-org/Rebus.RabbitMq/blob/10.1.1/Rebus.RabbitMq/RabbitMq/RabbitMqTransport.cs), and [RabbitMQ exchange and binding semantics](https://www.rabbitmq.com/docs/exchanges).

Azure Service Bus creates a topic for the event and a subscription named from the endpoint queue, then auto-forwards that subscription to the input queue. Its source handles concurrent topic/subscription creation by re-reading after a competing creator wins. Each distinct module queue therefore gets its own event copy; replicas using the same queue are competing consumers, not separate subscribers. Primary source: [`AzureServiceBusTransport.RegisterSubscriber`](https://github.com/rebus-org/Rebus.AzureServiceBus/blob/10.7.1/Rebus.AzureServiceBus/AzureServiceBus/AzureServiceBusTransport.cs); see also [Service Bus queue/topic/subscription semantics](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-queues-topics-subscriptions).

Queue names must be stable across replicas and deployments. Never append pod, process, or replica IDs. Repeating a subscription for the same endpoint queue maintains one logical subscriber; a different queue name intentionally creates another subscriber and another event copy.

## Lifecycle, failures, and isolation

`AddRebus` is managed through `IHostedService`; the generic host starts and stops it. `Rebus.ServiceProvider` stops workers when application stopping is signalled and disposes the bus during hosted-service shutdown. Each independent module endpoint follows that lifecycle. Primary sources: [`RebusInitializer`](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/ServiceProvider/Internals/RebusInitializer.cs) and [`RebusBackgroundService`](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/ServiceProvider/Internals/RebusBackgroundService.cs).

Rebus defaults to an error queue named `error` and five attempts. Override the error queue per module. Independent buses also isolate `SetNumberOfWorkers`, `SetMaxParallelism`, RabbitMQ `Prefetch`, and Azure Service Bus prefetch. This prevents a high-volume or slow module from spending every messaging worker in the process. Primary sources: [`RetryStrategySettings`](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Retry/Simple/RetryStrategySettings.cs), [`OptionsConfigurer`](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Config/OptionsConfigurer.cs), [`RabbitMqOptionsBuilder.Prefetch`](https://github.com/rebus-org/Rebus.RabbitMq/blob/10.1.1/Rebus.RabbitMq/Config/RabbitMqOptionsBuilder.cs), and [`AzureServiceBusTransportSettings.EnablePrefetching`](https://github.com/rebus-org/Rebus.AzureServiceBus/blob/10.7.1/Rebus.AzureServiceBus/Config/AzureServiceBusTransportSettings.cs).

Start with conservative explicit values and tune from measurements. Endpoint separation does not alter the at-least-once rule: every handler still uses its module-owned inbox and semantic idempotency, and every producer still dispatches its module-owned outbox through Rebus.

## Extraction-safe migration path

Per-module queues remove broker-topology churn from the mechanical part of extraction, but extraction is still deliberate work:

1. Eliminate or redesign each synchronous in-process dependency of the candidate module. A use case that needs immediate response remains an in-process dependency until it is explicitly redesigned; do not conceal it behind a fake local/remote bus abstraction.
2. Stabilize the candidate's integration contracts and prove inbox/outbox behavior, replay/bootstrap, and failure recovery while it is still in the monolith.
3. Move the module implementation, schema ownership, handlers, and outbox relay to a new process while retaining its queue name and event subscriptions.
4. Disable that module endpoint in the monolith before the extracted service takes ownership. Running both against the same queue makes them competing consumers; this is acceptable only for a deliberately compatible rolling cutover.
5. Reconfigure direct command routes only if the physical broker namespace changes. Publishers and unrelated subscribers continue using the same integration-event topics.
6. Run reconciliation after cutover. Queue continuity does not prove state-bootstrap completeness or database-extraction correctness.

No generic `IModuleBus`, transport-neutral routing DSL, or dual local/remote invocation proxy is required in v1. Stable message contracts, stable endpoint names, module-owned reliability tables, and explicit host composition are the useful extraction seams.

## Required proof tests

- Two module endpoints subscribe to the same integration event and each receives exactly one logical delivery; two replicas of one endpoint still produce one successful module effect.
- A failure in one module handler does not retry, block, or poison the other module endpoint.
- Each module's poison message reaches only its named error queue.
- RabbitMQ command routing and event fan-out behave identically at the application boundary after swapping the transport configuration to Azure Service Bus.
- Host shutdown stops intake and allows active handlers the configured shutdown interval.
- A cutover test disables the monolith endpoint and starts a separate worker against the same queue without losing or double-applying a business operation.
- Adding a new event consumer proves the separate bootstrap/watermark/reconciliation flow; it must not assume that subscribing replays history.

## Decision boundary

This research supports the following proposal:

1. One queue/bus per **actual asynchronous consumer module**, not one host-wide endpoint and not one queue per handler.
2. Separate `AddRebusService` providers for handler isolation; keyed buses remain available for other technical cases but are not module scopes.
3. Module-specific command routes, event subscriptions, error queues, concurrency, and prefetch settings.
4. Stable queue ownership as the extraction seam, while synchronous-call redesign and consumer state bootstrap remain explicit migration work.
