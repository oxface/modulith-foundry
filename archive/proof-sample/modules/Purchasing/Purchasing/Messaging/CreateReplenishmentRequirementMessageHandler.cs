using Microsoft.Extensions.Hosting;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Replenishment.Requests;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

namespace ModulithFoundry.Modules.Purchasing.Messaging;

internal sealed class CreateReplenishmentRequirementMessageHandler(
    ReceiveReplenishmentRequestHandler handler,
    IHostApplicationLifetime lifetime
) : IHandleMessages<CreateReplenishmentRequirementV1>
{
    public Task Handle(CreateReplenishmentRequirementV1 message)
    {
        var headers = MessageContext.Current.Headers;
        if (
            !headers.TryGetValue("producer-module", out string? producer)
            || producer != "sales"
            || !headers.TryGetValue(Headers.MessageId, out string? id)
            || !Guid.TryParse(id, out Guid identity)
            || identity != message.MessageId
            || !headers.TryGetValue(Headers.CorrelationId, out string? correlation)
            || !Guid.TryParse(correlation, out Guid processId)
            || processId != message.ProcessId
        )
            throw new InvalidDataException(
                "Replenishment request provenance or delivery identity is invalid."
            );
        return handler.HandleAsync(message, lifetime.ApplicationStopping);
    }
}
