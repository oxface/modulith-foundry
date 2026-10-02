using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Messaging.Persistence;
using ModulithFoundry.Modules.Sales.Orders;
using ModulithFoundry.Modules.Sales.Orders.Activity;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Fulfilment.RecordReservationOutcome;

internal sealed class RecordReservationOutcomeHandler(
    SalesDbContext context,
    TimeProvider timeProvider
)
{
    internal async Task HandleAsync(
        StockReservationOutcomeV1 outcome,
        CancellationToken cancellationToken
    )
    {
        Validate(outcome);
        context.UseWorkflowOrganization(outcome.OrganizationId);
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        string deliveryFingerprint = Hash(outcome);
        SalesInboxReceipt? receipt = await context.InboxReceipts.SingleOrDefaultAsync(
            item => item.MessageId == outcome.MessageId,
            cancellationToken
        );
        if (receipt is not null)
        {
            if (receipt.Fingerprint != deliveryFingerprint)
                throw new InvalidDataException(
                    "A message identity was reused with different content."
                );
            return;
        }
        DateTimeOffset timestamp = timeProvider.GetUtcNow();
        DateTimeOffset now = new(timestamp.UtcTicks - timestamp.UtcTicks % 10, TimeSpan.Zero);
        OrderFulfilmentProcess? process = await context.FulfilmentProcesses.SingleOrDefaultAsync(
            item => item.Id == outcome.ProcessId,
            cancellationToken
        );
        OrderFulfilmentLine? line = process?.Lines.SingleOrDefault(item =>
            item.LineNumber == outcome.LineNumber
        );
        bool matches =
            process is not null
            && process.OrderNumber == outcome.OrderNumber
            && line is not null
            && line.AttemptCount == 1
            && line.OperationId == outcome.OperationId
            && line.CommandMessageId == outcome.CausationId
            && line.Quantity == outcome.RequestedQuantity
            && line.BaseUnitCode == outcome.BaseUnitCode;
        if (!matches)
        {
            context.AuditEntries.Add(
                SalesAuditEntry.WorkflowDecision(
                    outcome.OrganizationId,
                    SalesAuditActions.ReservationOutcomeIgnored,
                    outcome.ProcessId,
                    SalesAuditOutcomes.Ignored,
                    ReservationOutcomeReasonCodes.CorrelationMismatch,
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
            // Delivery identity may change during an explicit replay; the retained business decision may not.
            string fingerprint = Hash(
                new
                {
                    outcome.CausationId,
                    outcome.OrganizationId,
                    outcome.OperationId,
                    outcome.ProcessId,
                    outcome.OrderNumber,
                    outcome.LineNumber,
                    outcome.Outcome,
                    outcome.ReservationId,
                    RequestedQuantity = outcome.RequestedQuantity.ToString(
                        "G29",
                        CultureInfo.InvariantCulture
                    ),
                    AvailableQuantity = outcome.AvailableQuantity.ToString(
                        "G29",
                        CultureInfo.InvariantCulture
                    ),
                    outcome.BaseUnitCode,
                    outcome.ReasonCode,
                }
            );
            if (line!.OutcomeFingerprint is { } accepted)
            {
                if (accepted != fingerprint)
                    throw new InvalidDataException(
                        "A reservation operation has conflicting outcomes."
                    );
            }
            else
            {
                OrderFulfilmentLineStatus status = outcome.Outcome switch
                {
                    StockReservationOutcome.Reserved => OrderFulfilmentLineStatus.Reserved,
                    StockReservationOutcome.Shortage => OrderFulfilmentLineStatus.Shortage,
                    StockReservationOutcome.Rejected => OrderFulfilmentLineStatus.Rejected,
                    _ => throw new InvalidDataException("Reservation outcome is unsupported."),
                };
                process!.ApplyOutcome(
                    line,
                    new(
                        status,
                        outcome.ReservationId,
                        outcome.AvailableQuantity,
                        outcome.ReasonCode,
                        fingerprint
                    )
                );
                long orderVersion = await context
                    .SalesOrders.Where(order => order.Id == process.OrderId)
                    .Select(order => order.Version)
                    .SingleAsync(cancellationToken);
                SalesOrderActivityKind kind = status switch
                {
                    OrderFulfilmentLineStatus.Reserved => SalesOrderActivityKind.StockReserved,
                    OrderFulfilmentLineStatus.Shortage => SalesOrderActivityKind.StockShortage,
                    _ => SalesOrderActivityKind.ReservationRejected,
                };
                context.OrderActivity.Add(
                    SalesOrderActivity.RecordFulfilment(
                        process,
                        orderVersion,
                        kind,
                        line.LineNumber,
                        now
                    )
                );
                context.AuditEntries.Add(
                    SalesAuditEntry.WorkflowDecision(
                        process.OrganizationId,
                        SalesAuditActions.ReservationOutcomeRecorded,
                        process.Id,
                        SalesAuditOutcomes.Succeeded,
                        null,
                        new
                        {
                            process.Version,
                            line.LineNumber,
                            line.OperationId,
                            line.ReservationId,
                            Status = OrderFulfilmentLineStatusValues.ToValue(line.Status),
                        },
                        now
                    )
                );
                if (status == OrderFulfilmentLineStatus.Shortage)
                    ReplenishmentCommandStaging.Stage(context, process, line, now);
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

    private static void Validate(StockReservationOutcomeV1 outcome)
    {
        if (
            outcome.MessageId == Guid.Empty
            || outcome.CausationId == Guid.Empty
            || outcome.OrganizationId == Guid.Empty
            || outcome.OperationId == Guid.Empty
            || outcome.ProcessId == Guid.Empty
            || outcome.OrderNumber <= 0
            || outcome.LineNumber <= 0
            || string.IsNullOrWhiteSpace(outcome.BaseUnitCode)
            || outcome.BaseUnitCode.Length > 16
            || outcome.ReasonCode?.Length > 100
            || outcome.AvailableQuantity < 0
            || outcome.AvailableQuantity > 9_999_999_999_999.999999m
            || decimal.Round(outcome.AvailableQuantity, 6) != outcome.AvailableQuantity
            || (
                outcome.Outcome == StockReservationOutcome.Shortage
                && outcome.AvailableQuantity >= outcome.RequestedQuantity
            )
        )
            throw new InvalidDataException("Reservation outcome metadata is invalid.");
        _ = OrderQuantity.Create(outcome.RequestedQuantity);
        bool valid = outcome.Outcome switch
        {
            StockReservationOutcome.Reserved => outcome.ReservationId is { } id
                && id != Guid.Empty
                && outcome.ReasonCode is null,
            StockReservationOutcome.Shortage or StockReservationOutcome.Rejected =>
                outcome.ReservationId is null && !string.IsNullOrWhiteSpace(outcome.ReasonCode),
            _ => false,
        };
        if (!valid)
            throw new InvalidDataException("Reservation outcome shape is invalid.");
    }
}
