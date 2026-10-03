using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class BrokerOperationalTests
{
    [Fact]
    public async Task Reservation_StorageFailure_ReportsNamedErrorQueueAndRedrivesOriginalIntent()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        using var broker = fixture.ObserveBroker();
        var command = fixture.Command(4m);
        await fixture.ExecuteSqlAsync(
            """
            CREATE FUNCTION inventory.reject_test_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test receipt fault'; END $$;
            CREATE TRIGGER reject_test_receipt BEFORE INSERT ON inventory.inbox_receipts
            FOR EACH ROW EXECUTE FUNCTION inventory.reject_test_receipt();
            """
        );
        await fixture.SendAsync(command);
        await broker.WaitAsync(
            InventoryMessaging.ErrorQueue,
            state => state.Ready == 1 && state.Unacknowledged == 0,
            fixture.CancellationToken
        );
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        await fixture.StartErrorObserverAsync();
        var failed = await fixture.ReadErrorAsync();
        Assert.Equal(command, failed);
        await fixture.ExecuteSqlAsync(
            """
            DROP TRIGGER reject_test_receipt ON inventory.inbox_receipts;
            DROP FUNCTION inventory.reject_test_receipt();
            """
        );
        await fixture.SendAsync(failed);
        var outcome = await fixture.ReadOutcomeAsync();
        Assert.Equal(command.OperationId, outcome.OperationId);
        Assert.Equal(StockReservationOutcome.Reserved, outcome.Outcome);
        await broker.WaitAsync(
            InventoryMessaging.ErrorQueue,
            state => state.Ready == 0 && state.Unacknowledged == 0,
            fixture.CancellationToken
        );
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task Reservation_ReceiverOffline_ReportsReadyThenUnacknowledgedUntilCommittedDeliverySettles()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        // The receiver declares its durable queue before going offline; a sender cannot create it.
        await fixture.StopReceiverAsync();
        using var broker = fixture.ObserveBroker();
        var command = fixture.Command(4m);
        await fixture.SendAsync(command);
        await broker.WaitAsync(
            InventoryMessaging.InputQueue,
            state => state.Ready == 1 && state.Unacknowledged == 0,
            fixture.CancellationToken
        );
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        await using var receiver = await ReceiverProcess.StartAsync(
            fixture,
            pauseAfterCommit: true
        );
        await receiver.WaitForSignalAsync(
            $"committed:{command.MessageId}",
            fixture.CancellationToken
        );
        await broker.WaitAsync(
            InventoryMessaging.InputQueue,
            state => state.Ready == 0 && state.Unacknowledged == 1,
            fixture.CancellationToken
        );
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        await receiver.KillAsync();
        await using var survivor = await ReceiverProcess.StartAsync(fixture);
        await survivor.WaitForSignalAsync(
            $"handled:{command.MessageId}",
            fixture.CancellationToken
        );
        await broker.WaitAsync(
            InventoryMessaging.InputQueue,
            state => state.Ready == 0 && state.Unacknowledged == 0,
            fixture.CancellationToken
        );
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }
}
