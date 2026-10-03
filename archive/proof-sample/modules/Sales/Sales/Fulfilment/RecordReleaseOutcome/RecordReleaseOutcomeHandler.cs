using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Messaging;
using ModulithFoundry.Modules.Sales.Messaging.Persistence;
using ModulithFoundry.Modules.Sales.Orders.Activity;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Fulfilment.RecordReleaseOutcome;

internal sealed class RecordReleaseOutcomeHandler(
    SalesDbContext context,
    TimeProvider clock,
    SalesMessagingMetrics metrics
)
{
    internal async Task HandleAsync(
        StockReservationReleaseOutcomeV1 outcome,
        CancellationToken cancellationToken
    )
    {
        Validate(outcome);
        context.UseWorkflowOrganization(outcome.OrganizationId);
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        string deliveryFingerprint = Hash(outcome);
        var receipt = await context.InboxReceipts.SingleOrDefaultAsync(
            item => item.MessageId == outcome.MessageId,
            cancellationToken
        );
        if (receipt is not null)
        {
            if (receipt.Fingerprint != deliveryFingerprint)
                throw new InvalidDataException(
                    "A release outcome identity was reused with different content."
                );
            metrics.InboxDuplicate();
            return;
        }
        var time = clock.GetUtcNow();
        DateTimeOffset now = new(time.UtcTicks - time.UtcTicks % 10, TimeSpan.Zero);
        var process = await context.FulfilmentProcesses.SingleOrDefaultAsync(
            item => item.Id == outcome.ProcessId,
            cancellationToken
        );
        var line = process?.Lines.SingleOrDefault(item => item.LineNumber == outcome.LineNumber);
        bool matches =
            process is { CancellationRequested: true }
            && process.OrderNumber == outcome.OrderNumber
            && line is not null
            && line.ReleaseCommandMessageId == outcome.CausationId
            && line.ReleaseOperationId == outcome.OperationId
            && line.OperationId == outcome.ReservationOperationId
            && line.ReservationId == outcome.ReservationId
            && (
                outcome.Outcome == StockReservationReleaseOutcome.Rejected
                || (
                    line.Quantity == outcome.ReservationQuantity
                    && line.BaseUnitCode == outcome.BaseUnitCode
                )
            );
        if (!matches)
        {
            context.AuditEntries.Add(
                SalesAuditEntry.WorkflowDecision(
                    outcome.OrganizationId,
                    SalesAuditActions.ReservationReleaseOutcomeIgnored,
                    outcome.ProcessId,
                    SalesAuditOutcomes.Ignored,
                    ReleaseOutcomeReasonCodes.CorrelationMismatch,
                    new
                    {
                        outcome.MessageId,
                        outcome.OperationId,
                        outcome.LineNumber,
                    },
                    now
                )
            );
        }
        else
        {
            string fingerprint = Hash(
                new
                {
                    outcome.CausationId,
                    outcome.OrganizationId,
                    outcome.OperationId,
                    outcome.ProcessId,
                    outcome.OrderNumber,
                    outcome.LineNumber,
                    outcome.ReservationOperationId,
                    outcome.ReservationId,
                    outcome.Outcome,
                    Quantity = outcome.ReservationQuantity?.ToString(
                        "G29",
                        CultureInfo.InvariantCulture
                    ),
                    outcome.BaseUnitCode,
                    outcome.ReasonCode,
                }
            );
            if (line!.ReleaseOutcomeFingerprint is { } accepted)
            {
                if (accepted != fingerprint)
                    throw new InvalidDataException("A release operation has conflicting outcomes.");
            }
            else
            {
                var previousStatus = process!.Status;
                var status = outcome.Outcome switch
                {
                    StockReservationReleaseOutcome.Released =>
                        OrderFulfilmentReleaseStatus.Released,
                    StockReservationReleaseOutcome.AlreadyReleased =>
                        OrderFulfilmentReleaseStatus.AlreadyReleased,
                    StockReservationReleaseOutcome.Rejected =>
                        OrderFulfilmentReleaseStatus.Rejected,
                    _ => throw new InvalidDataException("Release outcome is unsupported."),
                };
                process.ApplyRelease(line, status, outcome.ReasonCode, fingerprint);
                long orderVersion = await context
                    .SalesOrders.Where(order => order.Id == process.OrderId)
                    .Select(order => order.Version)
                    .SingleAsync(cancellationToken);
                context.OrderActivity.Add(
                    SalesOrderActivity.RecordFulfilment(
                        process,
                        orderVersion,
                        status == OrderFulfilmentReleaseStatus.Rejected
                            ? SalesOrderActivityKind.ReservationReleaseRejected
                            : SalesOrderActivityKind.ReservationReleased,
                        line.LineNumber,
                        now
                    )
                );
                context.AuditEntries.Add(
                    SalesAuditEntry.WorkflowDecision(
                        process.OrganizationId,
                        SalesAuditActions.ReservationReleaseOutcomeRecorded,
                        process.Id,
                        status == OrderFulfilmentReleaseStatus.Rejected
                            ? SalesAuditOutcomes.Rejected
                            : SalesAuditOutcomes.Succeeded,
                        outcome.ReasonCode,
                        new
                        {
                            process.Version,
                            line.LineNumber,
                            line.ReleaseOperationId,
                            line.ReservationId,
                            Status = OrderFulfilmentReleaseStatusValues.ToValue(status),
                        },
                        now
                    )
                );
                CompensationActivity.StageCompletion(
                    context,
                    process,
                    previousStatus,
                    orderVersion,
                    now
                );
            }
        }
        context.InboxReceipts.Add(
            SalesInboxReceipt.Processed(
                outcome.MessageId,
                outcome.OrganizationId,
                deliveryFingerprint,
                now
            )
        );
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string Hash<T>(T value) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    private static void Validate(StockReservationReleaseOutcomeV1 outcome)
    {
        if (
            outcome.MessageId == Guid.Empty
            || outcome.CausationId == Guid.Empty
            || outcome.OrganizationId == Guid.Empty
            || outcome.OperationId == Guid.Empty
            || outcome.ProcessId == Guid.Empty
            || outcome.ReservationOperationId == Guid.Empty
            || outcome.ReservationId == Guid.Empty
            || outcome.OrderNumber <= 0
            || outcome.LineNumber <= 0
            || outcome.ReasonCode?.Length > 100
            || outcome.BaseUnitCode?.Length > 16
        )
            throw new InvalidDataException("Release outcome metadata is invalid.");
        bool valid = outcome.Outcome switch
        {
            StockReservationReleaseOutcome.Released
            or StockReservationReleaseOutcome.AlreadyReleased => outcome.ReservationQuantity is > 0
                && !string.IsNullOrWhiteSpace(outcome.BaseUnitCode)
                && outcome.ReasonCode is null,
            StockReservationReleaseOutcome.Rejected => outcome.ReservationQuantity is null
                && outcome.BaseUnitCode is null
                && !string.IsNullOrWhiteSpace(outcome.ReasonCode),
            _ => false,
        };
        if (!valid)
            throw new InvalidDataException("Release outcome shape is invalid.");
        if (outcome.ReservationQuantity is { } quantity)
            _ = Orders.OrderQuantity.Create(quantity);
    }
}
