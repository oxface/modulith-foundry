using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal sealed class PurchaseOrderStore(
    PurchasingDbContext context,
    PurchaseOrderInlineProjection projection
)
{
    internal const string StreamType = "purchasing.purchase-order";
    internal const string CodeConstraint = "ux_purchase_order_write_models_organization_code";

    internal async Task<PurchaseOrderAggregate?> LoadForWritingAsync(
        OrganizationId organizationId,
        Guid id,
        long expectedVersion,
        CancellationToken cancellationToken
    )
    {
        RequireTransaction();
        if (expectedVersion < 1)
            throw new InvalidPurchaseOrderValueException(
                "expectedVersion",
                "Expected version must be positive."
            );
        var stream = await context.EventStreams.SingleOrDefaultAsync(
            x =>
                x.OrganizationId == organizationId.Value
                && x.Id == id
                && x.StreamType == StreamType,
            cancellationToken
        );
        if (stream is null)
            return null;
        if (stream.Version != expectedVersion)
            throw new PurchaseOrderConcurrencyException();
        var model = await context
            .PurchaseOrderWriteModels.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId.Value && x.StreamId == id,
                cancellationToken
            );
        if (model is null || model.Version < stream.Version)
            throw new PurchaseOrderIntegrityException(
                id,
                PurchaseOrderIntegrityFailure.WriteModelBehind
            );
        if (model.Version > stream.Version)
            throw new PurchaseOrderConcurrencyException();
        return PurchaseOrderAggregate.FromState(id, stream.Version, model.ToState());
    }

    internal async Task StageAppendAsync(
        OrganizationId organizationId,
        UserId actorUserId,
        PurchaseOrderAggregate aggregate,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        RequireTransaction();
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(actorUserId.Value, Guid.Empty);
        if (aggregate.Pending.Count == 0)
            return;
        if (aggregate.ExpectedVersion == 0)
            context.EventStreams.Add(
                EventStream.Open(
                    aggregate.Id,
                    organizationId.Value,
                    StreamType,
                    aggregate.Version,
                    now
                )
            );
        else
        {
            var stream = await context.EventStreams.SingleAsync(
                x => x.Id == aggregate.Id && x.OrganizationId == organizationId.Value,
                cancellationToken
            );
            stream.Advance(aggregate.ExpectedVersion, aggregate.Version, now);
        }
        var version = aggregate.ExpectedVersion;
        var traceId = Activity.Current?.TraceId.ToString();
        var metadata = JsonSerializer.SerializeToElement(
            new
            {
                OrganizationId = organizationId.Value,
                ActorUserId = actorUserId.Value,
                CorrelationId = traceId,
                CausationId = (string?)null,
                TraceId = traceId,
            }
        );
        foreach (var @event in aggregate.Pending)
        {
            var serialized = PurchaseOrderEventSerializer.Serialize(@event);
            context.Events.Add(
                StoredEvent.Create(
                    organizationId.Value,
                    aggregate.Id,
                    ++version,
                    serialized.Name,
                    serialized.Version,
                    serialized.Payload,
                    now,
                    metadata
                )
            );
        }
        await projection.StageAsync(organizationId.Value, aggregate, cancellationToken);
    }

    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "Purchase Order writes require an explicit transaction."
            );
    }

    internal static bool IsCodeConflict(DbUpdateException exception) =>
        exception.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: CodeConstraint
            };

    internal static bool IsVersionConflict(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException
        || exception.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_events_stream_version"
            };
}
