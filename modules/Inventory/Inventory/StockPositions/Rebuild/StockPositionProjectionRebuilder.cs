using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.ReferenceData;
using ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Rebuild;

internal sealed class StockPositionProjectionRebuilder(
    InventoryDbContext context,
    StockPositionEventReader reader,
    StockPositionWriteGate writeGate,
    InventoryRequestAuthorization authorization,
    TimeProvider timeProvider) : IStockPositionProjectionRebuilder
{
    public async Task<StockPositionRebuildResult> RebuildAsync(
        RebuildStockPositionProjectionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!authorization.MatchesContext(command.ActorUserId, command.OrganizationId))
        {
            return new StockPositionRebuildResult.PermissionDenied();
        }
        if (!await authorization.HasPermissionAsync(command.ActorUserId, command.OrganizationId,
            InventoryPermissionIds.ProjectionRebuild, cancellationToken))
        {
            context.AuditEntries.Add(InventoryAuditEntry.PermissionDenied(command.OrganizationId.Value, command.ActorUserId.Value,
                InventoryAuditActions.StockPositionRebuildDenied, InventoryAuditSubjectTypes.StockPosition, timeProvider.GetUtcNow()));
            await context.SaveChangesAsync(cancellationToken);
            return new StockPositionRebuildResult.PermissionDenied();
        }

        // Capture the head only after earlier writers finish. Later writers load after repair commits.
        await using var transaction = await writeGate.BeginAsync(command.OrganizationId.Value, exclusive: true, cancellationToken);
        EventStream? stream = await context.EventStreams.AsNoTracking().SingleOrDefaultAsync(
            stream => stream.Id == command.StockPositionId.Value && stream.StreamType == StockPositionStore.StreamType, cancellationToken);
        if (stream is null) { return new StockPositionRebuildResult.NotFound(); }

        StockPositionAggregate aggregate = await reader.LoadAtVersionAsync(stream.Id, stream.Version, cancellationToken)
            ?? throw new StockPositionIntegrityException(stream.Id, StockPositionIntegrityFailure.StreamMissing);
        DateTimeOffset recordedAt = await context.Events.AsNoTracking()
            .Where(stored => stored.StreamId == stream.Id && stored.StreamVersion == stream.Version)
            .Select(stored => stored.RecordedAt).SingleAsync(cancellationToken);
        StockPositionState state = aggregate.State
            ?? throw new StockPositionIntegrityException(stream.Id, StockPositionIntegrityFailure.InvalidEventSequence);
        StockPositionWriteModel? current = await context.StockPositionWriteModels
            .SingleOrDefaultAsync(current => current.StreamId == stream.Id, cancellationToken);
        if (current is not null) { await context.Entry(current).ReloadAsync(cancellationToken); }
        // Compare raw fields: corrupt persisted quantities need not pass domain value-object restoration.
        bool matched = current is not null && current.StockItemId == state.StockItemId
            && current.StockingLocationId == state.StockingLocationId && current.BaseUnitCode == state.BaseUnitCode
            && current.OnHandQuantity == state.OnHand.Value && current.ReservedQuantity == state.Reserved.Value
            && current.AvailableQuantity == state.Available.Value && current.Version == stream.Version
            && current.UpdatedAt == recordedAt;
        if (current is null)
        {
            context.StockPositionWriteModels.Add(StockPositionWriteModel.Create(
                stream.Id, command.OrganizationId.Value, state, stream.Version, recordedAt));
        }
        else { current.Restore(state, stream.Version, recordedAt); }

        context.AuditEntries.Add(InventoryAuditEntry.Succeeded(command.OrganizationId.Value, command.ActorUserId.Value,
            InventoryAuditActions.StockPositionProjectionRebuilt, InventoryAuditSubjectTypes.StockPosition, stream.Id,
            new { Version = stream.Version, PreviousModelMatched = matched }, timeProvider.GetUtcNow()));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new StockPositionRebuildResult.Rebuilt(command.StockPositionId, stream.Version, matched);
    }
}
