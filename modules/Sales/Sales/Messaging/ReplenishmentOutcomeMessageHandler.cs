using Microsoft.Extensions.Hosting;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Sales.Fulfilment.RecordReplenishmentOutcome;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

namespace ModulithFoundry.Modules.Sales.Messaging;

internal sealed class ReplenishmentOutcomeMessageHandler(
    RecordReplenishmentOutcomeHandler handler,
    IHostApplicationLifetime lifetime
)
    : IHandleMessages<ReplenishmentRequirementCreatedV1>,
        IHandleMessages<ReplenishmentRequestRejectedV1>
{
    public Task Handle(ReplenishmentRequirementCreatedV1 outcome)
    {
        ValidateHeaders(outcome.MessageId, outcome.ProcessId, outcome.CausationId);
        return handler.HandleAsync(outcome, lifetime.ApplicationStopping);
    }

    public Task Handle(ReplenishmentRequestRejectedV1 outcome)
    {
        ValidateHeaders(outcome.MessageId, outcome.ProcessId, outcome.CausationId);
        return handler.HandleAsync(outcome, lifetime.ApplicationStopping);
    }

    private static void ValidateHeaders(Guid messageId, Guid processId, Guid causationId)
    {
        var headers = MessageContext.Current.Headers;
        if (
            !headers.TryGetValue("producer-module", out string? producer)
            || producer != "purchasing"
            || !headers.TryGetValue(Headers.MessageId, out string? id)
            || id != messageId.ToString()
            || !headers.TryGetValue(Headers.CorrelationId, out string? correlation)
            || correlation != processId.ToString()
            || !headers.TryGetValue("causation-id", out string? cause)
            || cause != causationId.ToString()
        )
            throw new InvalidDataException("Replenishment outcome transport metadata is invalid.");
    }
}
