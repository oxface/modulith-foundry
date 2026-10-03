using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.StockItemProjection;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

namespace ModulithFoundry.Modules.Purchasing.Messaging;

internal sealed class StockItemReferenceChangedMessageHandler(
    RecordStockItemReferenceHandler handler,
    IHostApplicationLifetime lifetime,
    PurchasingMessagingMetrics metrics,
    ILogger<StockItemReferenceChangedMessageHandler> logger
) : IHandleMessages<StockItemReferenceChangedV1>
{
    public async Task Handle(StockItemReferenceChangedV1 message)
    {
        try
        {
            Dictionary<string, string> headers = MessageContext.Current.Headers;
            if (
                !headers.TryGetValue("producer-module", out string? producer)
                || producer != "inventory"
                || !headers.TryGetValue(Headers.MessageId, out string? messageId)
                || messageId != message.MessageId.ToString()
            )
                throw new InvalidDataException("Stock Item reference message metadata is invalid.");
            await handler.HandleAsync(message, lifetime.ApplicationStopping);
        }
        catch (OperationCanceledException)
            when (lifetime.ApplicationStopping.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            metrics.ProjectionProcessingFailed();
            PurchasingMessagingLogs.ProjectionProcessingFailed(
                logger,
                message.MessageId,
                exception.GetType().Name
            );
            throw;
        }
    }
}
