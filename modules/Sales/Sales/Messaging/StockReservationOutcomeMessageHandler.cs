using Microsoft.Extensions.Hosting;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Fulfilment.RecordReservationOutcome;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

namespace ModulithFoundry.Modules.Sales.Messaging;

internal sealed class StockReservationOutcomeMessageHandler(
    RecordReservationOutcomeHandler handler,
    IHostApplicationLifetime lifetime
) : IHandleMessages<StockReservationOutcomeV1>
{
    public Task Handle(StockReservationOutcomeV1 message)
    {
        var headers = MessageContext.Current.Headers;
        if (
            headers.GetValueOrDefault("producer-module") != "inventory"
            || headers.GetValueOrDefault(Headers.MessageId) != message.MessageId.ToString()
        )
            throw new InvalidDataException(
                "Reservation outcome producer/transport identity is invalid."
            );
        return handler.HandleAsync(message, lifetime.ApplicationStopping);
    }
}
