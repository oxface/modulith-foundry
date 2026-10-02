using Microsoft.Extensions.Hosting;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Fulfilment.RecordReleaseOutcome;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

namespace ModulithFoundry.Modules.Sales.Messaging;

internal sealed class StockReservationReleaseOutcomeMessageHandler(
    RecordReleaseOutcomeHandler handler,
    IHostApplicationLifetime lifetime
) : IHandleMessages<StockReservationReleaseOutcomeV1>
{
    public Task Handle(StockReservationReleaseOutcomeV1 message)
    {
        var headers = MessageContext.Current.Headers;
        if (
            headers.GetValueOrDefault("producer-module") != "inventory"
            || headers.GetValueOrDefault(Headers.MessageId) != message.MessageId.ToString()
            || headers.GetValueOrDefault(Headers.CorrelationId) != message.ProcessId.ToString()
            || headers.GetValueOrDefault("causation-id") != message.CausationId.ToString()
        )
            throw new InvalidDataException(
                "Release outcome producer/transport identity is invalid."
            );
        return handler.HandleAsync(message, lifetime.ApplicationStopping);
    }
}
