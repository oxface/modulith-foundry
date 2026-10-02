using Microsoft.Extensions.Hosting;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.StockItemProjection;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

namespace ModulithFoundry.Modules.Purchasing.Messaging;

internal sealed class StockItemReferenceChangedMessageHandler(
    RecordStockItemReferenceHandler handler,
    IHostApplicationLifetime lifetime
) : IHandleMessages<StockItemReferenceChangedV1>
{
    public Task Handle(StockItemReferenceChangedV1 message)
    {
        Dictionary<string, string> headers = MessageContext.Current.Headers;
        if (
            !headers.TryGetValue("producer-module", out string? producer)
            || producer != "inventory"
            || !headers.TryGetValue(Headers.MessageId, out string? messageId)
            || messageId != message.MessageId.ToString()
        )
            throw new InvalidDataException("Stock Item reference message metadata is invalid.");
        return handler.HandleAsync(message, lifetime.ApplicationStopping);
    }
}
