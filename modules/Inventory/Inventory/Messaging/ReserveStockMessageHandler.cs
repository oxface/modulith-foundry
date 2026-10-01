using Microsoft.Extensions.Hosting;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Reservations;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

namespace ModulithFoundry.Modules.Inventory.Messaging;

internal sealed class ReserveStockMessageHandler(
    ReserveStockHandler handler,
    IHostApplicationLifetime lifetime
) : IHandleMessages<ReserveStockV1>
{
    public Task Handle(ReserveStockV1 command)
    {
        Dictionary<string, string> headers = MessageContext.Current.Headers;
        if (
            !headers.TryGetValue("producer-module", out string? producer)
            || producer != "sales"
            || !headers.TryGetValue(Headers.MessageId, out string? messageId)
            || messageId != command.MessageId.ToString()
        )
            throw new InvalidDataException("Reservation message metadata is invalid.");

        return handler.HandleAsync(command, lifetime.ApplicationStopping);
    }
}
