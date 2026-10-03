using Microsoft.Extensions.Hosting;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Reservations;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

namespace ModulithFoundry.Modules.Inventory.Messaging;

internal sealed class ReleaseReservationMessageHandler(
    ReleaseReservationHandler handler,
    IHostApplicationLifetime lifetime
) : IHandleMessages<ReleaseReservationV1>
{
    public Task Handle(ReleaseReservationV1 command)
    {
        var headers = MessageContext.Current.Headers;
        if (
            !headers.TryGetValue("producer-module", out string? producer)
            || producer != "sales"
            || !headers.TryGetValue(Headers.MessageId, out string? messageId)
            || messageId != command.MessageId.ToString()
            || !headers.TryGetValue(Headers.CorrelationId, out string? correlationId)
            || correlationId != command.ProcessId.ToString()
        )
            throw new InvalidDataException("Release message metadata is invalid.");
        return handler.HandleAsync(command, lifetime.ApplicationStopping);
    }
}
