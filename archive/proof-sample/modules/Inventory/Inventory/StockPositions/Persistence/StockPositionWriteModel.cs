using System.Text.Json;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

internal sealed class StockPositionWriteModel : IOrganizationOwned
{
    private StockPositionWriteModel()
    {
        BaseUnitCode = null!;
    }

    private StockPositionWriteModel(
        Guid streamId,
        Guid organizationId,
        Guid stockItemId,
        Guid stockingLocationId,
        string baseUnitCode,
        decimal onHandQuantity,
        decimal reservedQuantity,
        decimal availableQuantity,
        long version,
        DateTimeOffset updatedAt
    )
    {
        StreamId = streamId;
        OrganizationId = organizationId;
        StockItemId = stockItemId;
        StockingLocationId = stockingLocationId;
        BaseUnitCode = baseUnitCode;
        OnHandQuantity = onHandQuantity;
        ReservedQuantity = reservedQuantity;
        AvailableQuantity = availableQuantity;
        Version = version;
        UpdatedAt = updatedAt;
    }

    internal Guid StreamId { get; private set; }

    public Guid OrganizationId { get; private set; }

    internal Guid StockItemId { get; private set; }

    internal Guid StockingLocationId { get; private set; }

    internal string BaseUnitCode { get; private set; }

    internal decimal OnHandQuantity { get; private set; }

    internal decimal ReservedQuantity { get; private set; }

    internal decimal AvailableQuantity { get; private set; }

    internal long Version { get; private set; }

    internal DateTimeOffset UpdatedAt { get; private set; }

    internal JsonElement Reservations { get; private set; } =
        JsonSerializer.SerializeToElement(Array.Empty<StockReservationState>());

    internal StockPositionState ToState()
    {
        try
        {
            return new(
                StockItemId,
                StockingLocationId,
                BaseUnitCode,
                Quantity.Restore(OnHandQuantity),
                Quantity.Restore(ReservedQuantity),
                Reservations.Deserialize<StockReservationState[]>() ?? []
            );
        }
        catch (JsonException exception)
        {
            throw new StockPositionIntegrityException(
                StreamId,
                StockPositionIntegrityFailure.WriteModelUnreadable,
                observedVersion: Version,
                innerException: exception
            );
        }
    }

    internal static StockPositionWriteModel Create(
        Guid streamId,
        Guid organizationId,
        StockPositionState state,
        long version,
        DateTimeOffset updatedAt
    ) =>
        new StockPositionWriteModel(
            streamId,
            organizationId,
            state.StockItemId,
            state.StockingLocationId,
            state.BaseUnitCode,
            state.OnHand.Value,
            state.Reserved.Value,
            state.Available.Value,
            version,
            updatedAt
        )
        {
            Reservations = JsonSerializer.SerializeToElement(state.Reservations ?? []),
        };

    internal void Update(StockPositionState state, long version, DateTimeOffset updatedAt)
    {
        if (StockItemId != state.StockItemId || StockingLocationId != state.StockingLocationId)
        {
            throw new InvalidOperationException(
                "Stock Position projection identity cannot change."
            );
        }

        BaseUnitCode = state.BaseUnitCode;
        OnHandQuantity = state.OnHand.Value;
        ReservedQuantity = state.Reserved.Value;
        AvailableQuantity = state.Available.Value;
        Version = version;
        UpdatedAt = updatedAt;
        Reservations = JsonSerializer.SerializeToElement(state.Reservations ?? []);
    }

    internal void Restore(StockPositionState state, long version, DateTimeOffset updatedAt)
    {
        StockItemId = state.StockItemId;
        StockingLocationId = state.StockingLocationId;
        Update(state, version, updatedAt);
    }
}
