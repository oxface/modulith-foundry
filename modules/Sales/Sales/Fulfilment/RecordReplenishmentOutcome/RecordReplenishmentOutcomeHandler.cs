using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Messaging;
using ModulithFoundry.Modules.Sales.Messaging.Persistence;
using ModulithFoundry.Modules.Sales.Orders.Activity;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Fulfilment.RecordReplenishmentOutcome;

internal sealed class RecordReplenishmentOutcomeHandler(
    SalesDbContext context,
    TimeProvider clock,
    SalesMessagingMetrics metrics
)
{
    internal Task HandleAsync(
        ReplenishmentRequirementCreatedV1 outcome,
        CancellationToken cancellationToken
    )
    {
        if (outcome.RequirementId == Guid.Empty || outcome.RequirementNumber <= 0)
            throw new InvalidDataException("Replenishment requirement identity is invalid.");
        return RecordAsync(
            outcome.MessageId,
            outcome.CausationId,
            outcome.OrganizationId,
            outcome.OperationId,
            outcome.ProcessId,
            outcome.OrderNumber,
            outcome.LineNumber,
            outcome.StockItemId,
            outcome.Quantity,
            outcome.BaseUnitCode,
            outcome.RequirementId,
            outcome.RequirementNumber,
            null,
            Hash(outcome),
            cancellationToken
        );
    }

    internal Task HandleAsync(
        ReplenishmentRequestRejectedV1 outcome,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(outcome.ReasonCode) || outcome.ReasonCode.Length > 100)
            throw new InvalidDataException("Replenishment rejection reason is invalid.");
        return RecordAsync(
            outcome.MessageId,
            outcome.CausationId,
            outcome.OrganizationId,
            outcome.OperationId,
            outcome.ProcessId,
            outcome.OrderNumber,
            outcome.LineNumber,
            outcome.StockItemId,
            outcome.Quantity,
            outcome.BaseUnitCode,
            null,
            null,
            outcome.ReasonCode,
            Hash(outcome),
            cancellationToken
        );
    }

    private async Task RecordAsync(
        Guid messageId,
        Guid causationId,
        Guid organizationId,
        Guid operationId,
        Guid processId,
        long orderNumber,
        int lineNumber,
        Guid stockItemId,
        decimal quantity,
        string unit,
        Guid? requirementId,
        long? requirementNumber,
        string? reason,
        string deliveryFingerprint,
        CancellationToken cancellationToken
    )
    {
        if (
            messageId == Guid.Empty
            || causationId == Guid.Empty
            || organizationId == Guid.Empty
            || operationId == Guid.Empty
            || processId == Guid.Empty
            || stockItemId == Guid.Empty
            || orderNumber <= 0
            || lineNumber <= 0
            || string.IsNullOrWhiteSpace(unit)
            || unit.Length > 16
        )
            throw new InvalidDataException("Replenishment outcome metadata is invalid.");
        _ = ModulithFoundry.Modules.Sales.Orders.OrderQuantity.Create(quantity);
        context.UseWorkflowOrganization(organizationId);
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        var receipt = await context.InboxReceipts.SingleOrDefaultAsync(
            x => x.MessageId == messageId,
            cancellationToken
        );
        if (receipt is not null)
        {
            if (receipt.Fingerprint != deliveryFingerprint)
                throw new InvalidDataException(
                    "Replenishment delivery identity has conflicting content."
                );
            metrics.InboxDuplicate();
            return;
        }
        var process = await context.FulfilmentProcesses.SingleOrDefaultAsync(
            x => x.Id == processId,
            cancellationToken
        );
        var line = process?.Lines.SingleOrDefault(x => x.LineNumber == lineNumber);
        bool matches =
            process is not null
            && process.OrderNumber == orderNumber
            && line is not null
            && line.ReplenishmentCommandMessageId == causationId
            && line.OperationId == operationId
            && line.StockItemId == stockItemId
            && line.ReplenishmentQuantity == quantity
            && line.BaseUnitCode == unit
            && line.Status == OrderFulfilmentLineStatus.Shortage;
        var time = clock.GetUtcNow();
        DateTimeOffset now = new(time.UtcTicks - time.UtcTicks % 10, TimeSpan.Zero);
        if (!matches)
            context.AuditEntries.Add(
                SalesAuditEntry.WorkflowDecision(
                    organizationId,
                    SalesAuditActions.ReplenishmentOutcomeIgnored,
                    processId,
                    SalesAuditOutcomes.Ignored,
                    ReplenishmentOutcomeReasonCodes.CorrelationMismatch,
                    new
                    {
                        messageId,
                        operationId,
                        lineNumber,
                    },
                    now
                )
            );
        else
        {
            string semantic = Hash(
                new
                {
                    causationId,
                    organizationId,
                    operationId,
                    processId,
                    orderNumber,
                    lineNumber,
                    stockItemId,
                    Quantity = quantity.ToString("G29", CultureInfo.InvariantCulture),
                    unit,
                    requirementId,
                    requirementNumber,
                    reason,
                }
            );
            if (line!.ReplenishmentOutcomeFingerprint is { } accepted)
            {
                if (accepted != semantic)
                    throw new InvalidDataException(
                        "A replenishment operation has conflicting outcomes."
                    );
            }
            else
            {
                process!.ApplyReplenishment(
                    line,
                    requirementId,
                    requirementNumber,
                    reason,
                    semantic
                );
                long orderVersion = await context
                    .SalesOrders.Where(x => x.Id == process.OrderId)
                    .Select(x => x.Version)
                    .SingleAsync(cancellationToken);
                context.OrderActivity.Add(
                    SalesOrderActivity.RecordFulfilment(
                        process,
                        orderVersion,
                        reason is null
                            ? SalesOrderActivityKind.ReplenishmentCreated
                            : SalesOrderActivityKind.ReplenishmentRejected,
                        lineNumber,
                        now
                    )
                );
                context.AuditEntries.Add(
                    SalesAuditEntry.WorkflowDecision(
                        organizationId,
                        SalesAuditActions.ReplenishmentOutcomeRecorded,
                        processId,
                        SalesAuditOutcomes.Succeeded,
                        reason,
                        new
                        {
                            lineNumber,
                            operationId,
                            requirementId,
                            requirementNumber,
                            process.Version,
                        },
                        now
                    )
                );
            }
        }
        context.InboxReceipts.Add(
            SalesInboxReceipt.Processed(messageId, organizationId, deliveryFingerprint, now)
        );
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string Hash<T>(T value) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
