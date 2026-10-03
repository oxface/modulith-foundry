using System.Text.Json;
using ModulithFoundry.Modules.Inventory.StockPositions;
using ModulithFoundry.Modules.Inventory.StockPositions.Events;
using ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

namespace ModulithFoundry.ApplicationTests;

public sealed class StockPositionEventCompatibilityTests
{
    [Fact]
    public void ReadPersistedV1Fixtures_ReconstructsStateWithoutClrStorageIdentity()
    {
        Guid streamId = Guid.NewGuid();
        IStockPositionEvent[] events =
        [
            ReadFixture("opened.v1.json", streamId, version: 1),
            ReadFixture("received.v1.json", streamId, version: 2),
        ];

        StockPositionAggregate aggregate = StockPositionAggregate.Rehydrate(streamId, events);

        Assert.Equal(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            aggregate.State!.StockItemId
        );
        Assert.Equal(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            aggregate.State.StockingLocationId
        );
        Assert.Equal("EA", aggregate.State.BaseUnitCode);
        Assert.Equal(10.125m, aggregate.State.OnHand.Value);
        Assert.Equal(2, aggregate.Version);
        Assert.Empty(aggregate.UncommittedEvents);
    }

    [Theory]
    [InlineData(
        "inventory.stock-position.quantity-corrected",
        1,
        "{\"onHandQuantity\":7.5}",
        (int)StockPositionIntegrityFailure.InvalidEventPayload
    )]
    [InlineData(
        "inventory.stock-position.quantity-corrected",
        1,
        "{\"onHandQuantity\":7.5,\"reason\":null}",
        (int)StockPositionIntegrityFailure.InvalidEventPayload
    )]
    [InlineData(
        "inventory.stock-position.received",
        99,
        "{\"quantity\": 1}",
        (int)StockPositionIntegrityFailure.UnknownEvent
    )]
    [InlineData(
        "old.clr.namespace.StockReceived",
        1,
        "{\"quantity\": 1}",
        (int)StockPositionIntegrityFailure.UnknownEvent
    )]
    [InlineData(
        "inventory.stock-position.received",
        1,
        "{}",
        (int)StockPositionIntegrityFailure.InvalidEventPayload
    )]
    [InlineData(
        "inventory.stock-position.received",
        1,
        "null",
        (int)StockPositionIntegrityFailure.InvalidEventPayload
    )]
    [InlineData(
        "inventory.stock-position.received",
        1,
        "{\"quantity\": \"bad\"}",
        (int)StockPositionIntegrityFailure.InvalidEventPayload
    )]
    [InlineData(
        "inventory.stock-position.opened",
        1,
        "{\"stockItemId\":\"11111111-1111-1111-1111-111111111111\",\"stockingLocationId\":\"22222222-2222-2222-2222-222222222222\",\"baseUnitCode\":null}",
        (int)StockPositionIntegrityFailure.InvalidEventPayload
    )]
    public void Deserialize_UnsupportedOrMalformedEnvelope_RaisesIntegrityFault(
        string eventName,
        int schemaVersion,
        string payload,
        int expectedFailure
    )
    {
        Guid streamId = Guid.NewGuid();
        using JsonDocument document = JsonDocument.Parse(payload);
        StoredEvent stored = StoredEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            streamId,
            2,
            eventName,
            schemaVersion,
            DateTimeOffset.UtcNow,
            document.RootElement.Clone(),
            default
        );

        StockPositionIntegrityException failure = Assert.Throws<StockPositionIntegrityException>(
            () =>
                StockPositionEventSerializer.Deserialize(stored)
        );

        Assert.Equal(streamId, failure.StreamId);
        Assert.Equal(2, failure.ObservedVersion);
        Assert.Equal((StockPositionIntegrityFailure)expectedFailure, failure.Failure);
    }

    [Fact]
    public void ReadPersistedCorrectionFixture_ReconcilesQuantityWithoutPendingEvents()
    {
        Guid streamId = Guid.NewGuid();
        StockPositionAggregate aggregate = StockPositionAggregate.Rehydrate(
            streamId,
            [
                ReadFixture("opened.v1.json", streamId, version: 1),
                ReadFixture("received.v1.json", streamId, version: 2),
                ReadFixture("quantity-corrected.v1.json", streamId, version: 3),
            ]
        );

        Assert.Equal(7.5m, aggregate.State!.OnHand.Value);
        Assert.Equal(7.5m, aggregate.State.Available.Value);
        Assert.Equal(3, aggregate.Version);
        Assert.Empty(aggregate.UncommittedEvents);
    }

    [Fact]
    public void ReadPersistedReservationFixture_ReconstructsReservationIdentityAndQuantity()
    {
        Guid streamId = Guid.NewGuid();
        StockPositionAggregate aggregate = StockPositionAggregate.Rehydrate(
            streamId,
            [
                ReadFixture("opened.v1.json", streamId, 1),
                ReadFixture("received.v1.json", streamId, 2),
                ReadFixture("reserved.v1.json", streamId, 3),
            ]
        );

        Assert.Equal(4m, aggregate.State!.Reserved.Value);
        Assert.Equal(6.125m, aggregate.State.Available.Value);
        StockReservationState reservation = Assert.Single(aggregate.State.Reservations!);
        Assert.Equal(Guid.Parse("33333333-3333-3333-3333-333333333333"), reservation.ReservationId);
        Assert.Equal(Guid.Parse("44444444-4444-4444-4444-444444444444"), reservation.OperationId);
        Assert.Empty(aggregate.UncommittedEvents);
    }

    private static IStockPositionEvent ReadFixture(string fileName, Guid streamId, long version)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "StockPositionEvents",
            fileName
        );
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(path));
        StoredEvent stored = StoredEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            streamId,
            version,
            fixture.RootElement.GetProperty("eventName").GetString()!,
            fixture.RootElement.GetProperty("schemaVersion").GetInt32(),
            DateTimeOffset.UtcNow,
            fixture.RootElement.GetProperty("payload").Clone(),
            default
        );
        return StockPositionEventSerializer.Deserialize(stored);
    }
}
