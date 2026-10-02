using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Purchasing.Audit;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Messaging.Persistence;
using ModulithFoundry.Modules.Purchasing.Persistence;

namespace ModulithFoundry.Modules.Purchasing.Replenishment.Requests;

internal sealed class ReceiveReplenishmentRequestHandler(
    PurchasingDbContext context,
    ReplenishmentRequestProcessor processor,
    TimeProvider timeProvider
)
{
    internal async Task HandleAsync(
        CreateReplenishmentRequirementV1 command,
        CancellationToken cancellationToken
    )
    {
        Validate(command);
        context.UseWorkflowOrganization(command.OrganizationId);
        string semantic = Hash(
            new
            {
                command.OrganizationId,
                command.OperationId,
                command.ProcessId,
                command.OrderNumber,
                command.LineNumber,
                command.StockItemId,
                Quantity = command.Quantity.ToString("G29", CultureInfo.InvariantCulture),
                command.BaseUnitCode,
                command.MinimumReferenceRevision,
            }
        );
        string delivery = Hash(
            new
            {
                command.MessageId,
                Semantic = semantic,
                CreatedAt = command.CreatedAt.ToUniversalTime(),
            }
        );
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        var checkpoint = (
            await context
                .StockItemBootstrapCheckpoints.FromSqlRaw(
                    "SELECT * FROM purchasing.stock_item_bootstrap WHERE id = 1 FOR UPDATE"
                )
                .ToListAsync(cancellationToken)
        ).Single();
        PurchasingInboxReceipt? receipt = await context
            .InboxReceipts.IgnoreQueryFilters([PurchasingDbContext.OrganizationScopeFilter])
            .SingleOrDefaultAsync(x => x.MessageId == command.MessageId, cancellationToken);
        if (receipt is not null)
        {
            if (receipt.Fingerprint != delivery || receipt.Rejected)
                throw new InvalidDataException(
                    "Replenishment delivery identity conflicts or was rejected."
                );
            return;
        }
        ReplenishmentRequest? request = await context
            .ReplenishmentRequests.IgnoreQueryFilters([PurchasingDbContext.OrganizationScopeFilter])
            .SingleOrDefaultAsync(x => x.OperationId == command.OperationId, cancellationToken);
        DateTimeOffset timestamp = timeProvider.GetUtcNow();
        DateTimeOffset now = new(timestamp.UtcTicks - timestamp.UtcTicks % 10, TimeSpan.Zero);
        bool conflict = request is not null && request.Fingerprint != semantic;
        if (conflict)
            context.AuditEntries.Add(
                PurchasingAuditEntry.Record(
                    command.OrganizationId,
                    PurchasingAuditActions.RequestRejected,
                    command.OperationId,
                    PurchasingAuditOutcomes.Rejected,
                    ReplenishmentRequestReasonCodes.OperationConflict,
                    new { command.MessageId },
                    now
                )
            );
        else if (request is null)
        {
            request = ReplenishmentRequest.Create(command, semantic, now);
            context.ReplenishmentRequests.Add(request);
            context.AuditEntries.Add(
                PurchasingAuditEntry.Record(
                    command.OrganizationId,
                    PurchasingAuditActions.RequestAccepted,
                    command.OperationId,
                    PurchasingAuditOutcomes.Succeeded,
                    null,
                    new { command.ProcessId, command.LineNumber },
                    now
                )
            );
            await processor.ResolveAsync(request, checkpoint.IsReady, now, cancellationToken);
        }
        context.InboxReceipts.Add(
            PurchasingInboxReceipt.Processed(
                command.MessageId,
                command.OrganizationId,
                delivery,
                now,
                rejected: conflict
            )
        );
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        // Persist the rejection once; subsequent Rebus attempts stay poison without duplicate audit.
        if (conflict)
            throw new InvalidDataException(
                "Replenishment operation identity was reused with different intent."
            );
    }

    private static string Hash<T>(T value) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    private static void Validate(CreateReplenishmentRequirementV1 command)
    {
        if (
            command.MessageId == Guid.Empty
            || command.OrganizationId == Guid.Empty
            || command.OperationId == Guid.Empty
            || command.ProcessId == Guid.Empty
            || command.StockItemId == Guid.Empty
            || command.OrderNumber <= 0
            || command.LineNumber <= 0
            || command.MinimumReferenceRevision < 0
            || string.IsNullOrWhiteSpace(command.BaseUnitCode)
            || command.BaseUnitCode.Length > 16
        )
            throw new InvalidDataException("Replenishment request metadata is invalid.");
        _ = ReplenishmentQuantity.Create(command.Quantity);
    }
}
