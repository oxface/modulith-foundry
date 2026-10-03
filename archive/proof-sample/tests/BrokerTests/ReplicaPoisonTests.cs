namespace ModulithFoundry.BrokerTests;

public sealed class ReplicaPoisonTests
{
    [Fact]
    public async Task Reservation_TwoLiveReceivers_QuarantinesPoisonWithinLocalAttemptBounds()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        await fixture.StopReceiverAsync();
        using var broker = fixture.ObserveBroker();
        await fixture.ExecuteSqlAsync(
            """
            CREATE FUNCTION inventory.reject_test_poison() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'private-test-poison'; END $$;
            CREATE TRIGGER reject_test_poison BEFORE INSERT ON inventory.inbox_receipts
            FOR EACH ROW EXECUTE FUNCTION inventory.reject_test_poison();
            """
        );
        await using var first = await ReceiverProcess.StartAsync(fixture);
        await using var second = await ReceiverProcess.StartAsync(fixture);
        var command = fixture.Command(4m);
        await fixture.SendAsync(command);
        await broker.WaitAsync(
            ModulithFoundry.Modules.Inventory.Composition.InventoryMessaging.ErrorQueue,
            state => state.Ready == 1 && state.Unacknowledged == 0,
            fixture.CancellationToken
        );
        int firstAttempts = first.FailedAttempts(command.MessageId);
        int secondAttempts = second.FailedAttempts(command.MessageId);
        Assert.InRange(firstAttempts, 0, 3);
        Assert.InRange(secondAttempts, 0, 3);
        Assert.InRange(firstAttempts + secondAttempts, 3, 6);
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(2, (await fixture.StockAsync()).Version);
        await fixture.StartErrorObserverAsync();
        Assert.Equal(command, await fixture.ReadErrorAsync());
        await fixture.ExecuteSqlAsync(
            """
            DROP TRIGGER reject_test_poison ON inventory.inbox_receipts;
            DROP FUNCTION inventory.reject_test_poison();
            """
        );
        await fixture.SendAsync(command);
        Assert.Equal(command.OperationId, (await fixture.ReadOutcomeAsync()).OperationId);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }
}
