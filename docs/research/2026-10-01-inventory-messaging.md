# Inventory messaging API and delivery evidence

Research date: 2026-10-01. Primary-source API inspection uses Rebus tag `8.9.5`, Rebus.ServiceProvider tag `10.7.2`, and Rebus.RabbitMq tag `10.1.1`. This note establishes implementation constraints, not a new architecture decision.

## Version and license checks

| Component | Verified version | License | Primary evidence |
| --- | --- | --- | --- |
| Rebus | 8.9.4 exists; prefer current stable 8.9.5 | MIT | [NuGet](https://www.nuget.org/packages/Rebus/8.9.5), [changelog](https://github.com/rebus-org/Rebus/blob/8.9.5/CHANGELOG.md) |
| Rebus.RabbitMq | 10.1.1 | MIT | [NuGet](https://www.nuget.org/packages/Rebus.RabbitMq/10.1.1), [tagged project](https://github.com/rebus-org/Rebus.RabbitMq/blob/10.1.1/Rebus.RabbitMq/Rebus.RabbitMq.csproj) |
| Rebus.ServiceProvider | 10.7.2 | MIT | [NuGet](https://www.nuget.org/packages/Rebus.ServiceProvider/10.7.2), [tagged project](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/Rebus.ServiceProvider.csproj) |
| Testcontainers.RabbitMq | 4.15.0 | MIT | [NuGet](https://www.nuget.org/packages/Testcontainers.RabbitMq/4.15.0) |
| Aspire.Hosting.RabbitMQ | 13.5.4 | MIT | [NuGet](https://www.nuget.org/packages/Aspire.Hosting.RabbitMQ/13.5.4) |
| RabbitMQ server | 4.3.6 | MPL-2.0 for server/core plugins; some OCF files Apache-2.0 | [release](https://github.com/rabbitmq/rabbitmq-server/releases/tag/v4.3.6), [tagged license](https://github.com/rabbitmq/rabbitmq-server/blob/v4.3.6/LICENSE) |

Rebus 8.9.4 fixes a worker stall under a synchronization context. 8.9.5, published September 29, fixes error-handler failures that could stall consumers or lose messages, ensuring unacknowledged messages are NACKed. Both adapter packages require Rebus >= 8.9.0, so 8.9.5 satisfies their dependency constraints. Aspire's RabbitMQ adapter is aligned to the repository's 13.5.4 hosting baseline. [Rebus changelog](https://github.com/rebus-org/Rebus/blob/8.9.5/CHANGELOG.md), [adapter dependencies](https://www.nuget.org/packages/Rebus.ServiceProvider/10.7.2), [transport dependencies](https://www.nuget.org/packages/Rebus.RabbitMq/10.1.1).

## Isolated endpoint registration and handler scopes

The verified isolation entry point is `IHostBuilder.AddRebusService(Action<IServiceCollection>, params Type[])`, available through `builder.Host` on `WebApplicationBuilder`. Its callback builds an independent provider and must register `AddRebus`. There is no `IServiceCollection.AddRebusService` extension in 10.7.2. The explicit forwarding overload forwards exactly its supplied types; it does **not** append the defaults from the no-types overload. To forward the data source and preserve host logging/lifetime, use:

```csharp
builder.Host.AddRebusService(
    endpointServices => RegisterInventoryEndpoint(endpointServices),
    typeof(NpgsqlDataSource),
    typeof(ILoggerFactory),
    typeof(IHostApplicationLifetime));
```

`NpgsqlDataSource` is the sole forwarded application dependency here. Forwarded registrations are singleton instances owned by the host. Register tenant accessors, repositories, and handlers in the isolated provider. [Tagged hosting source](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/Config/HostBuilderExtensions.cs).

`AddRebusHandler<T>()` registers transient handlers. The handler activator creates an async scope for the incoming transaction, reuses a scope already saved in the incoming step context, and disposes its created scope when the transaction is disposed. A mutable per-delivery tenant accessor should therefore be scoped. A pipeline step must not resolve that accessor from `context.Load<IServiceProvider>()`: the provider step saves the endpoint's root provider. Before handler activation, a tenant step can create an async scope, save `AsyncServiceScope?` in the context, register its disposal, set the scoped accessor, and let the activator reuse it. Alternatively the handler itself can initialize its injected scoped accessor. [Activator](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/ServiceProvider/DependencyInjectionHandlerActivator.cs), [handler registrations](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/Config/ServiceCollectionExtensions.cs), [provider step](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/ServiceProvider/ServiceProviderProviderStep.cs).

## Stable message aliases and topic names are separate

### Registration is not reconfiguration

`AddRebus` rejects a second default bus in the same service collection. Multiple non-default keyed buses are supported but do not isolate handlers. Every `AddRebusService` call adds another hosted service with a fresh independent provider; it does not deduplicate registrations or replace an existing endpoint. Register each module endpoint once. Different module queues/providers can coexist; accidentally registering Inventory twice starts additional competing consumers and relays, not an idempotent no-op. See [default-bus guard](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/Config/ServiceCollectionExtensions.cs) and [independent-provider registration](https://github.com/rebus-org/Rebus.ServiceProvider/blob/10.7.2/Rebus.ServiceProvider/Config/HostBuilderExtensions.cs).

Broker declaration is a separate concern: RabbitMQ permits redeclaration of a queue with equivalent attributes, but incompatible attributes produce `406 PRECONDITION_FAILED`. A different queue name creates another queue, not a migration/rename of retained messages or bindings. [RabbitMQ property equivalence](https://www.rabbitmq.com/docs/queues#declaration-and-property-equivalence).

Use the built-in JSON serializer and explicit aliases:

```csharp
using Rebus.Serialization.Custom;
using Rebus.Serialization.Json;

configure.Serialization(s =>
{
    s.UseSystemTextJson();
    s.UseCustomMessageTypeNames()
        .AddWithCustomName<InventoryReservedV1>("inventory.reserved.v1");
});
```

This maps `Headers.Type` in both directions, including deserialization to the known CLR contract. Unknown types/names fail unless `AllowFallbackToDefaultConvention()` is deliberately enabled. Do not enable fallback for the external contract boundary. `UseSystemTextJson(JsonSerializerOptions settings, Encoding encoding = null)` is the options overload. [Alias extension](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Serialization/Custom/CustomTypeNameConventionExtensions.cs), [alias builder](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Serialization/Custom/CustomTypeNameConventionBuilder.cs), [JSON configuration](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Serialization/Json/SystemJsonConfigurationExtensions.cs).

Typed `Publish`/`Subscribe` obtains topic names from a different service, `Rebus.Topic.ITopicNameConvention`, whose API is `string GetTopic(Type eventType)`. Register an explicit mapping with `configure.Options(o => o.Register<ITopicNameConvention>(_ => convention))` on producer and consumer. A serializer alias alone does not stabilize topic routing. Alternatively use the advanced explicit-topic APIs. [Convention](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Topic/ITopicNameConvention.cs), [publish and subscribe implementation](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Bus/RebusBus.cs).

## IDs, publish confirmation, retries, and settlement

Supply `Headers.MessageId` explicitly from the durable outbox record on every dispatch attempt:

```csharp
await bus.Publish(contract, new Dictionary<string, string>
{
    [Headers.MessageId] = outboxMessageId.ToString(),
    ["tenant-id"] = tenantId.ToString(),
});
```

Rebus preserves supplied IDs; otherwise its outgoing step generates a new GUID. A new `Publish` call without the header therefore gets a new ID even when the application regards it as a retry. The RabbitMQ receiver preserves the Rebus header across broker redelivery. [Default headers](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Pipeline/Send/AssignDefaultHeadersStep.cs), [transport receive conversion](https://github.com/rebus-org/Rebus.RabbitMq/blob/10.1.1/Rebus.RabbitMq/RabbitMq/RabbitMqTransport.cs).

Configure `.Transport(t => t.UseRabbitMq(connectionString, queue).SetPublisherConfirms(true))`. The 10.1.1 transport enables confirmation and confirmation tracking on its ordinary publisher channel and awaits `BasicPublishAsync`. Express messages use a channel without confirmations; do not mark outbox messages express. Its `SetPublisherConfirms(bool, TimeSpan)` API exists, but the transport stores the timeout without applying it to `BasicPublishAsync`; do not promise that this option bounds publish latency. [Transport source](https://github.com/rebus-org/Rebus.RabbitMq/blob/10.1.1/Rebus.RabbitMq/RabbitMq/RabbitMqTransport.cs), [options](https://github.com/rebus-org/Rebus.RabbitMq/blob/10.1.1/Rebus.RabbitMq/Config/RabbitMqOptionsBuilder.cs).

For an outbox dispatcher outside an ambient Rebus transaction, awaited `Publish` also awaits its private transaction's completion, including outgoing sends. Inside a Rebus handler/transaction it only enqueues outgoing messages until that transaction completes. Publisher confirmation establishes broker acceptance, not consumer success or a useful subscription binding; an unroutable publish can be confirmed. Therefore establish subscriptions before dispatch and only mark the outbox record dispatched after the awaited ordinary publish succeeds. [Bus implementation](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Bus/RebusBus.cs), [queued transport sends](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Transport/AbstractRebusTransport.cs), [RabbitMQ confirmation semantics](https://www.rabbitmq.com/docs/confirms).

The exact bounded retry API is `.Options(o => o.RetryStrategy(errorQueueName: "inventory.errors", maxDeliveryAttempts: 5))`, with `using Rebus.Retry.Simple;`. Normal exceptions retry and then reach the error queue; fail-fast classifications can bypass retries. The default tracker is in memory, so process restarts/competing consumers do not provide one durable global retry counter. [Retry configuration](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Retry/Simple/RetryStrategyConfigurationExtensions.cs), [retry step](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Retry/Simple/DefaultRetryStep.cs).

The worker awaits the receive pipeline before completing its transaction. Completion commits queued transport operations, then ACKs or NACKs. RabbitMQ registers `BasicAckAsync` and `BasicNackAsync(..., requeue: true)` as those callbacks. A handler's committed database transaction is consequently earlier than broker ACK; application inbox and business writes must commit together. [Worker](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Workers/ThreadPoolBased/ThreadPoolWorker.cs), [transaction](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Transport/TransactionContext.cs), [RabbitMQ transport](https://github.com/rebus-org/Rebus.RabbitMq/blob/10.1.1/Rebus.RabbitMq/RabbitMq/RabbitMqTransport.cs).

## Test-only fault after database commit and before ACK

Decorate the test endpoint pipeline only:

```csharp
o.Decorate<IPipeline>(c => new PipelineStepInjector(c.Get<IPipeline>())
    .OnReceive(faultStep, PipelineRelativePosition.Before,
        typeof(DispatchIncomingMessageStep)));
```

The step's `Process(IncomingStepContext context, Func<Task> next)` should `await next()` and throw once after the handler has returned. At that point the handler's inbox/business database commit has completed, but the worker has not ACKed. The outer retry step catches the injected failure and NACKs; the real broker redelivers the same ID. Assert multiple handler deliveries and exactly one committed business transition. Restrict the fault to the target message ID/type and use an atomic one-shot flag so unrelated deliveries cannot consume the injection. [Pipeline injector](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Pipeline/PipelineStepInjector.cs), [dispatcher](https://github.com/rebus-org/Rebus/blob/8.9.5/Rebus/Pipeline/Receive/DispatchIncomingMessageStep.cs).

This is a deterministic delivery-window fault test, not proof of process crash recovery. A true crash test needs a separate endpoint process terminated after an external database-commit signal and before settlement, then restarted; unacknowledged deliveries requeue on connection closure. Ordinary graceful bus disposal waits for workers and can ACK successfully, so it is not an equivalent crash mechanism. [RabbitMQ connection-loss redelivery](https://www.rabbitmq.com/docs/confirms).
