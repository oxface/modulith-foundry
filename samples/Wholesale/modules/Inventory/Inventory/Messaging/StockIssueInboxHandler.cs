using ModulithFoundry.Samples.Wholesale.Inventory.Contracts;
using Rootbolt.Messaging;
using Rootbolt.Messaging.EntityFrameworkCore;
using Rootbolt.Tenancy;

namespace ModulithFoundry.Samples.Wholesale.Inventory.Messaging;

internal sealed class StockIssueInboxHandler(
    ITenantContextInitializer tenancy,
    InventoryMessageContext metadata,
    IStockPositionCommands commands,
    IOutbox<InventoryDbContext> outbox,
    StockIssueMessages messages
) : IInboxHandler<InventoryDbContext>
{
    public async Task HandleAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        var command = StockIssueMessageAdmission.Validate(message);
        tenancy.Initialize(TenantContext.ForTenant(new TenantId(message.TenantKey!)));
        metadata.Initialize(message);

        var result = await commands.IssueAsync(
            new(
                command.StockPositionId,
                command.ExpectedVersion,
                [new StockIssue(command.Quantity)]
            ),
            cancellationToken
        );
        if (result is StockPositionChangeResult.Changed)
            return;

        var reason = result switch
        {
            StockPositionChangeResult.NotFound => StockIssueDeclineReason.NotFound,
            StockPositionChangeResult.Conflict => StockIssueDeclineReason.Conflict,
            StockPositionChangeResult.InsufficientStock =>
                StockIssueDeclineReason.InsufficientStock,
            _ => throw new InvalidOperationException("Unknown stock issue decision."),
        };
        var shortage = result as StockPositionChangeResult.InsufficientStock;
        outbox.Enqueue(
            messages.StockIssueDeclined(command, reason, shortage?.Available, shortage?.Requested)
        );
    }
}
