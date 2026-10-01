using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Messaging.Persistence;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.ReferenceData;
using ModulithFoundry.Modules.Inventory.StockPositions;
using ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

namespace ModulithFoundry.Modules.Inventory.Reservations;

internal sealed class ReserveStockHandler(
    InventoryDbContext context,
    StockPositionStore store,
    TimeProvider timeProvider
)
{
    internal async Task HandleAsync(ReserveStockV1 command, CancellationToken cancellationToken)
    {
        Validate(command);
        context.UseWorkflowOrganization(command.OrganizationId);
        await using var transaction = await store.BeginWriteAsync(
            new(command.OrganizationId),
            cancellationToken
        );
        string deliveryFingerprint = Hash(command);
        InventoryInboxReceipt? receipt = await context.InboxReceipts.SingleOrDefaultAsync(
            item => item.MessageId == command.MessageId,
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
        string operationFingerprint = Hash(
            new
            {
                command.OrganizationId,
                command.OperationId,
                command.ProcessId,
                command.OrderNumber,
                command.LineNumber,
                command.StockItemId,
                command.StockingLocationId,
                Quantity = command.Quantity.ToString("G29", CultureInfo.InvariantCulture),
                command.BaseUnitCode,
            }
        );
        ReservationOperation? existing = await context.ReservationOperations.SingleOrDefaultAsync(
            item => item.OperationId == command.OperationId,
            cancellationToken
        );
        if (existing is null)
        {
            StockReservationOutcomeV1 outcome = await DecideAsync(command, now, cancellationToken);
            context.ReservationOperations.Add(
                ReservationOperation.Complete(operationFingerprint, outcome)
            );
            context.OutboxMessages.Add(InventoryOutboxMessage.Stage(outcome));
            context.AuditEntries.Add(
                InventoryAuditEntry.WorkflowDecision(
                    command.OrganizationId,
                    ReservationAuditActions.Decided,
                    command.OperationId,
                    outcome.Outcome == StockReservationOutcome.Reserved ? "succeeded" : "rejected",
                    outcome.ReasonCode,
                    new
                    {
                        command.ProcessId,
                        command.LineNumber,
                        command.Quantity,
                        outcome.Outcome,
                    },
                    now
                )
            );
        }
        else if (existing.Fingerprint != operationFingerprint)
        {
            StockReservationOutcomeV1 conflict = Outcome(
                command,
                StockReservationOutcome.Rejected,
                null,
                0m,
                ReservationReasonCodes.OperationConflict,
                now
            );
            context.OutboxMessages.Add(InventoryOutboxMessage.Stage(conflict));
            context.AuditEntries.Add(
                InventoryAuditEntry.WorkflowDecision(
                    command.OrganizationId,
                    ReservationAuditActions.ConflictDenied,
                    command.OperationId,
                    "denied",
                    ReservationReasonCodes.OperationConflict,
                    new { command.ProcessId, command.LineNumber },
                    now
                )
            );
        }
        context.InboxReceipts.Add(
            InventoryInboxReceipt.Processed(
                command.MessageId,
                command.OrganizationId,
                deliveryFingerprint,
                now
            )
        );
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<StockReservationOutcomeV1> DecideAsync(
        ReserveStockV1 command,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var item = await context
            .StockItems.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == command.StockItemId, cancellationToken);
        var location = await context
            .StockingLocations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == command.StockingLocationId, cancellationToken);
        if (item is null || !item.IsActive || location is null || !location.IsActive)
            return Outcome(
                command,
                StockReservationOutcome.Rejected,
                null,
                0m,
                ReservationReasonCodes.ReferenceUnavailable,
                now
            );
        if (item.BaseUnitCode != command.BaseUnitCode)
            return Outcome(
                command,
                StockReservationOutcome.Rejected,
                null,
                0m,
                ReservationReasonCodes.UnitMismatch,
                now
            );
        StockPositionWriteModel? model = await context
            .StockPositionWriteModels.AsNoTracking()
            .SingleOrDefaultAsync(
                position =>
                    position.StockItemId == command.StockItemId
                    && position.StockingLocationId == command.StockingLocationId,
                cancellationToken
            );
        if (model is null)
            return Outcome(
                command,
                StockReservationOutcome.Shortage,
                null,
                0m,
                ReservationReasonCodes.InsufficientStock,
                now
            );
        StockPositionAggregate aggregate = await store.LoadForWritingAsync(
            command.StockItemId,
            command.StockingLocationId,
            model.Version,
            cancellationToken
        );
        Guid reservationId = Guid.CreateVersion7(now);
        decimal available = aggregate.State!.Available.Value;
        if (!aggregate.TryReserve(reservationId, command.OperationId, command.Quantity))
            return Outcome(
                command,
                StockReservationOutcome.Shortage,
                null,
                available,
                ReservationReasonCodes.InsufficientStock,
                now
            );
        await store.StageAppendAsync(
            new OrganizationId(command.OrganizationId),
            new StockPositionEventMetadata(
                command.OrganizationId,
                null,
                command.ProcessId.ToString(),
                command.MessageId.ToString(),
                Activity.Current?.TraceId.ToString(),
                "sales.order-fulfilment"
            ),
            aggregate,
            now,
            cancellationToken
        );
        return Outcome(
            command,
            StockReservationOutcome.Reserved,
            reservationId,
            aggregate.State!.Available.Value,
            null,
            now
        );
    }

    private static StockReservationOutcomeV1 Outcome(
        ReserveStockV1 command,
        StockReservationOutcome outcome,
        Guid? reservationId,
        decimal available,
        string? reason,
        DateTimeOffset now
    ) =>
        new(
            Guid.CreateVersion7(now),
            command.MessageId,
            command.OrganizationId,
            command.OperationId,
            command.ProcessId,
            command.OrderNumber,
            command.LineNumber,
            outcome,
            reservationId,
            command.Quantity,
            available,
            command.BaseUnitCode,
            reason,
            now
        );

    private static string Hash<T>(T value) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    private static void Validate(ReserveStockV1 command)
    {
        if (
            command.MessageId == Guid.Empty
            || command.OrganizationId == Guid.Empty
            || command.OperationId == Guid.Empty
            || command.ProcessId == Guid.Empty
            || command.StockItemId == Guid.Empty
            || command.StockingLocationId == Guid.Empty
            || command.OrderNumber <= 0
            || command.LineNumber <= 0
            || string.IsNullOrWhiteSpace(command.BaseUnitCode)
            || command.BaseUnitCode.Length > 16
        )
            throw new InvalidDataException("Reservation command identifiers are invalid.");
        _ = Quantity.Positive(command.Quantity);
    }
}
