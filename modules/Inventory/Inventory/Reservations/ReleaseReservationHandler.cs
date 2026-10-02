using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Messaging;
using ModulithFoundry.Modules.Inventory.Messaging.Persistence;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.ReferenceData;
using ModulithFoundry.Modules.Inventory.StockPositions;
using ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

namespace ModulithFoundry.Modules.Inventory.Reservations;

internal sealed class ReleaseReservationHandler(
    InventoryDbContext context,
    StockPositionStore store,
    TimeProvider clock,
    InventoryMessagingMetrics metrics
)
{
    internal async Task HandleAsync(
        ReleaseReservationV1 command,
        CancellationToken cancellationToken
    )
    {
        Validate(command);
        context.UseWorkflowOrganization(command.OrganizationId);
        await using var transaction = await store.BeginWriteAsync(
            new(command.OrganizationId),
            cancellationToken
        );
        string deliveryFingerprint = Hash(command);
        var receipt = await context.InboxReceipts.SingleOrDefaultAsync(
            item => item.MessageId == command.MessageId,
            cancellationToken
        );
        if (receipt is not null)
        {
            if (receipt.Fingerprint != deliveryFingerprint)
                throw new InvalidDataException(
                    "A release message identity was reused with different content."
                );
            metrics.InboxDuplicate();
            return;
        }
        var timestamp = clock.GetUtcNow();
        DateTimeOffset now = new(timestamp.UtcTicks - timestamp.UtcTicks % 10, TimeSpan.Zero);
        string operationFingerprint = Hash(
            new
            {
                command.OrganizationId,
                command.OperationId,
                command.ProcessId,
                command.OrderNumber,
                command.LineNumber,
                command.ReservationOperationId,
                command.ReservationId,
                command.StockItemId,
                command.StockingLocationId,
            }
        );
        var existing = await context.ReservationReleaseOperations.SingleOrDefaultAsync(
            item => item.OperationId == command.OperationId,
            cancellationToken
        );
        if (existing is null)
        {
            var outcome = await DecideAsync(command, now, cancellationToken);
            context.ReservationReleaseOperations.Add(
                ReservationReleaseOperation.Complete(operationFingerprint, outcome)
            );
            context.OutboxMessages.Add(InventoryOutboxMessage.Stage(outcome));
            context.AuditEntries.Add(
                InventoryAuditEntry.WorkflowDecision(
                    command.OrganizationId,
                    ReservationReleaseAuditActions.Decided,
                    command.OperationId,
                    outcome.Outcome == StockReservationReleaseOutcome.Rejected
                        ? "rejected"
                        : "succeeded",
                    outcome.ReasonCode,
                    new
                    {
                        command.ProcessId,
                        command.LineNumber,
                        command.ReservationOperationId,
                        command.ReservationId,
                        outcome.Outcome,
                    },
                    now
                )
            );
        }
        else if (existing.Fingerprint != operationFingerprint)
        {
            context.OutboxMessages.Add(
                InventoryOutboxMessage.Stage(
                    Outcome(
                        command,
                        StockReservationReleaseOutcome.Rejected,
                        null,
                        null,
                        ReservationReleaseReasonCodes.OperationConflict,
                        now
                    )
                )
            );
            context.AuditEntries.Add(
                InventoryAuditEntry.WorkflowDecision(
                    command.OrganizationId,
                    ReservationReleaseAuditActions.ConflictDenied,
                    command.OperationId,
                    "denied",
                    ReservationReleaseReasonCodes.OperationConflict,
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

    private async Task<StockReservationReleaseOutcomeV1> DecideAsync(
        ReleaseReservationV1 command,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var original = await context
            .ReservationOperations.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OperationId == command.ReservationOperationId,
                cancellationToken
            );
        if (original is null)
            return Outcome(
                command,
                StockReservationReleaseOutcome.Rejected,
                null,
                null,
                ReservationReleaseReasonCodes.ReservationUnavailable,
                now
            );
        StockReservationOutcomeV1 reserved;
        try
        {
            reserved =
                original.Outcome.Deserialize<StockReservationOutcomeV1>()
                ?? throw new ReservationIntegrityException(
                    command.ReservationOperationId,
                    ReservationIntegrityFailure.StoredOutcomeUnreadable
                );
        }
        catch (JsonException exception)
        {
            throw new ReservationIntegrityException(
                command.ReservationOperationId,
                ReservationIntegrityFailure.StoredOutcomeUnreadable,
                exception
            );
        }
        if (reserved.Outcome != StockReservationOutcome.Reserved)
            return Outcome(
                command,
                StockReservationReleaseOutcome.Rejected,
                null,
                null,
                ReservationReleaseReasonCodes.ReservationUnavailable,
                now
            );
        if (
            reserved.OrganizationId != command.OrganizationId
            || reserved.OperationId != command.ReservationOperationId
            || reserved.ReservationId is null
            || reserved.RequestedQuantity <= 0
            || string.IsNullOrWhiteSpace(reserved.BaseUnitCode)
        )
            throw new ReservationIntegrityException(
                command.ReservationOperationId,
                ReservationIntegrityFailure.StoredOutcomeUnreadable
            );
        if (
            reserved.ReservationId != command.ReservationId
            || reserved.ProcessId != command.ProcessId
            || reserved.OrderNumber != command.OrderNumber
            || reserved.LineNumber != command.LineNumber
        )
            return Outcome(
                command,
                StockReservationReleaseOutcome.Rejected,
                null,
                null,
                ReservationReleaseReasonCodes.CorrelationMismatch,
                now
            );

        // Deactivation prevents new reservations, not cleanup of an existing commitment.
        var model =
            await context
                .StockPositionWriteModels.AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.StockItemId == command.StockItemId
                        && item.StockingLocationId == command.StockingLocationId,
                    cancellationToken
                )
            ?? throw new ReservationIntegrityException(
                command.ReservationOperationId,
                ReservationIntegrityFailure.WriteModelMissing
            );
        var aggregate = await store.LoadForWritingAsync(
            command.StockItemId,
            command.StockingLocationId,
            model.Version,
            cancellationToken
        );
        if (
            (aggregate.State!.Reservations ?? [])
                .Where(item => !item.IsReleased)
                .Sum(item => item.Quantity) != aggregate.State.Reserved.Value
        )
            throw new ReservationIntegrityException(
                command.ReservationOperationId,
                ReservationIntegrityFailure.ReservationStateMismatch
            );
        var reservation = aggregate.State!.Reservations?.SingleOrDefault(item =>
            item.ReservationId == command.ReservationId
        );
        if (
            reservation is null
            || reservation.OperationId != command.ReservationOperationId
            || reservation.Quantity != reserved.RequestedQuantity
            || aggregate.State.BaseUnitCode != reserved.BaseUnitCode
        )
            throw new ReservationIntegrityException(
                command.ReservationOperationId,
                ReservationIntegrityFailure.ReservationStateMismatch
            );
        bool changed = aggregate.ReleaseReservation(command.ReservationId, command.OperationId);
        if (changed)
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
            changed
                ? StockReservationReleaseOutcome.Released
                : StockReservationReleaseOutcome.AlreadyReleased,
            reservation.Quantity,
            aggregate.State.BaseUnitCode,
            null,
            now
        );
    }

    private static StockReservationReleaseOutcomeV1 Outcome(
        ReleaseReservationV1 command,
        StockReservationReleaseOutcome outcome,
        decimal? quantity,
        string? unit,
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
            command.ReservationOperationId,
            command.ReservationId,
            outcome,
            quantity,
            unit,
            reason,
            now
        );

    private static string Hash<T>(T value) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    private static void Validate(ReleaseReservationV1 command)
    {
        if (
            command.MessageId == Guid.Empty
            || command.OrganizationId == Guid.Empty
            || command.OperationId == Guid.Empty
            || command.ProcessId == Guid.Empty
            || command.ReservationOperationId == Guid.Empty
            || command.ReservationId == Guid.Empty
            || command.StockItemId == Guid.Empty
            || command.StockingLocationId == Guid.Empty
            || command.OrderNumber <= 0
            || command.LineNumber <= 0
            || command.OperationId == command.ReservationOperationId
        )
            throw new InvalidDataException("Release command identifiers are invalid.");
    }
}
