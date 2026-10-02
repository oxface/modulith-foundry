using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.ReferenceData;

namespace ModulithFoundry.Modules.Inventory.Messaging.Recovery;

internal sealed class InventoryMessageDeliveryRecovery(
    InventoryDbContext context,
    InventoryRequestAuthorization authorization,
    TimeProvider clock
) : IInventoryMessageDeliveryRecovery
{
    public async Task<GetInventoryMessageDeliveryResult> GetAsync(
        GetInventoryMessageDeliveryQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        if (
            !await authorization.HasPermissionAsync(
                query.ActorUserId,
                query.OrganizationId,
                InventoryPermissionIds.MessageDeliveryRecover,
                cancellationToken
            )
        )
            return new GetInventoryMessageDeliveryResult.PermissionDenied();
        var message = await context
            .OutboxMessages.AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.MessageId == query.MessageId
                    && item.OrganizationId == query.OrganizationId.Value,
                cancellationToken
            );
        return message is null
            ? new GetInventoryMessageDeliveryResult.NotFound()
            : new GetInventoryMessageDeliveryResult.Found(message.ToView());
    }

    public async Task<RequeueInventoryMessageResult> RequeueAsync(
        RequeueInventoryMessageCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!authorization.MatchesContext(command.ActorUserId, command.OrganizationId))
            return new RequeueInventoryMessageResult.PermissionDenied();
        if (
            !await authorization.HasPermissionAsync(
                command.ActorUserId,
                command.OrganizationId,
                InventoryPermissionIds.MessageDeliveryRecover,
                cancellationToken
            )
        )
        {
            context.AuditEntries.Add(
                InventoryAuditEntry.PermissionDenied(
                    command.OrganizationId.Value,
                    command.ActorUserId.Value,
                    InventoryAuditActions.MessageDeliveryRecoveryDenied,
                    InventoryAuditSubjectTypes.MessageDelivery,
                    clock.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            return new RequeueInventoryMessageResult.PermissionDenied();
        }
        if (command.ExpectedDispatchAttempts < 0)
            return new RequeueInventoryMessageResult.InvalidExpectedDispatchAttempts();
        string reason = command.Reason?.Trim() ?? "";
        if (reason.Length is < 1 or > 500 || reason.Any(char.IsControl))
            return new RequeueInventoryMessageResult.InvalidReason();

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        // Shares the relay's row lock. Do not steal an active publication lease or let a
        // delayed dispatch mark overwrite an operator's replacement scheduling state.
        var messages = await context
            .OutboxMessages.FromSqlInterpolated(
                $"""
                SELECT * FROM inventory.outbox_messages
                WHERE message_id = {command.MessageId} AND organization_id = {command.OrganizationId.Value}
                FOR UPDATE
                """
            )
            .ToListAsync(cancellationToken);
        var message = messages.SingleOrDefault();
        if (message is null)
            return new RequeueInventoryMessageResult.NotFound();
        // A reused scoped context may already track an older dispatch generation. The row
        // lock protects the refresh from both relay claims and dispatch marking.
        await context.Entry(message).ReloadAsync(cancellationToken);
        if (
            message.MessageType
            is not (
                StockReservationOutcomeV1.LogicalName
                or StockReservationReleaseOutcomeV1.LogicalName
            )
        )
            return new RequeueInventoryMessageResult.UnsupportedMessageType();
        if (message.Attempts != command.ExpectedDispatchAttempts)
            return new RequeueInventoryMessageResult.VersionConflict();
        var now = await context
            .Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        if (message.LeaseUntil > now)
            return new RequeueInventoryMessageResult.InFlight();
        if (
            message.DispatchedAt is null
            && message.AvailableAt <= now
            && message.LeaseToken is null
        )
            return new RequeueInventoryMessageResult.AlreadyQueued(message.ToView());
        var previousPublishedAt = message.DispatchedAt;
        var previousAvailableAt = message.AvailableAt;
        message.Requeue(now);
        context.AuditEntries.Add(
            InventoryAuditEntry.Succeeded(
                command.OrganizationId.Value,
                command.ActorUserId.Value,
                InventoryAuditActions.MessageDeliveryRequeued,
                InventoryAuditSubjectTypes.MessageDelivery,
                message.MessageId,
                new
                {
                    message.MessageType,
                    message.Attempts,
                    PreviousPublishedAt = previousPublishedAt,
                    PreviousAvailableAt = previousAvailableAt,
                    Reason = reason,
                },
                now
            )
        );
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RequeueInventoryMessageResult.Requeued(message.ToView());
    }
}
