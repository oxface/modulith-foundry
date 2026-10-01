using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class InventoryReservationTests
{
    [Fact]
    public async Task ReserveStock_AvailableQuantity_CommitsReservationAndPublishesOutcome()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        ReserveStockV1 command = fixture.Command(4m);
        await fixture.SendAsync(command);
        StockReservationOutcomeV1 outcome = await fixture.ReadOutcomeAsync();
        Assert.Equal(StockReservationOutcome.Reserved, outcome.Outcome);
        Assert.Equal(command.OperationId, outcome.OperationId);
        Assert.Equal(command.MessageId, outcome.CausationId);
        Assert.NotNull(outcome.ReservationId);
        StockPositionView stock = await fixture.StockAsync();
        Assert.Equal(10m, stock.OnHandQuantity);
        Assert.Equal(4m, stock.ReservedQuantity);
        Assert.Equal(6m, stock.AvailableQuantity);
        Assert.Equal(3, stock.Version);
    }

    [Fact]
    public async Task ReserveStock_Rebuild_ReconstructsStateAndHistoryWithoutPublishingAgain()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        await fixture.SendAsync(fixture.Command(4m));
        await fixture.ReadOutcomeAsync();
        StockPositionView stock = await fixture.StockAsync();
        await fixture.RebuildAsync(stock.StockPositionId);
        Assert.Equal(stock, await fixture.StockAsync());
        StockPositionHistoryEntry reservation = Assert.Single(
            (await fixture.HistoryAsync()).Entries,
            entry => entry.Action == StockPositionHistoryAction.Reserved
        );
        Assert.Equal(4m, reservation.Quantity);
        Assert.Equal(3, reservation.Version);
        ReserveStockV1 marker = fixture.Command(100m);
        await fixture.SendAsync(marker);
        Assert.Equal(marker.OperationId, (await fixture.ReadOutcomeAsync()).OperationId);
    }

    [Fact]
    public async Task ReserveStock_InsufficientQuantity_RecordsWholeLineShortageWithoutStockEffect()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        await fixture.SendAsync(fixture.Command(11m));
        StockReservationOutcomeV1 shortage = await fixture.ReadOutcomeAsync();
        Assert.Equal(StockReservationOutcome.Shortage, shortage.Outcome);
        Assert.Null(shortage.ReservationId);
        Assert.Equal(10m, shortage.AvailableQuantity);
        Assert.Equal(11m, shortage.RequestedQuantity);
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(2, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task ReserveStock_RepeatedDeliveryAndOperation_ChangesStockOnlyOnce()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        ReserveStockV1 command = fixture.Command(4m);
        await fixture.SendAsync(command);
        StockReservationOutcomeV1 original = await fixture.ReadOutcomeAsync();
        await fixture.WaitForProcessedAsync(command.MessageId);
        await fixture.SendAsync(command);
        await fixture.WaitForProcessedAsync(command.MessageId, count: 2);
        ReserveStockV1 repeated = command with
        {
            MessageId = Guid.CreateVersion7(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await fixture.SendAsync(repeated);
        await fixture.WaitForProcessedAsync(repeated.MessageId);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
        ReserveStockV1 marker = fixture.Command(100m);
        await fixture.SendAsync(marker);
        Assert.Equal(marker.OperationId, (await fixture.ReadOutcomeAsync()).OperationId);
        Assert.Equal(StockReservationOutcome.Reserved, original.Outcome);
    }

    [Fact]
    public async Task ReserveStock_ConflictingOperation_RejectsReuseWithoutChangingOriginalEffect()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        ReserveStockV1 command = fixture.Command(4m);
        await fixture.SendAsync(command);
        await fixture.ReadOutcomeAsync();
        await fixture.SendAsync(command with { MessageId = Guid.CreateVersion7(), Quantity = 5m });
        StockReservationOutcomeV1 conflict = await fixture.ReadOutcomeAsync();
        Assert.Equal(StockReservationOutcome.Rejected, conflict.Outcome);
        Assert.Equal("operation-conflict", conflict.ReasonCode);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task ReserveStock_CompetingRequests_DoesNotOversell()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        await Task.WhenAll(
            fixture.SendAsync(fixture.Command(7m)),
            fixture.SendAsync(fixture.Command(7m))
        );
        StockReservationOutcomeV1[] outcomes =
        [
            await fixture.ReadOutcomeAsync(),
            await fixture.ReadOutcomeAsync(),
        ];
        Assert.Single(outcomes, outcome => outcome.Outcome == StockReservationOutcome.Reserved);
        Assert.Single(outcomes, outcome => outcome.Outcome == StockReservationOutcome.Shortage);
        Assert.Equal(7m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3m, (await fixture.StockAsync()).AvailableQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task ReserveStock_FailureAfterDatabaseCommit_RedeliversWithoutRepeatingEffect()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        ReserveStockV1 command = fixture.Command(4m);
        fixture.FailSettlementOnce(command.MessageId);
        await fixture.SendAsync(command);
        await fixture.ReadOutcomeAsync();
        await fixture.WaitForProcessedAsync(command.MessageId, count: 2);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
        ReserveStockV1 marker = fixture.Command(100m);
        await fixture.SendAsync(marker);
        Assert.Equal(marker.OperationId, (await fixture.ReadOutcomeAsync()).OperationId);
    }

    [Fact]
    public async Task ReserveStock_EquivalentDecimalRepresentation_IsSameBusinessOperation()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        ReserveStockV1 command = fixture.Command(4m);
        await fixture.SendAsync(command);
        await fixture.ReadOutcomeAsync();
        ReserveStockV1 equivalent = command with
        {
            MessageId = Guid.CreateVersion7(),
            Quantity = 4.000m,
        };
        await fixture.SendAsync(equivalent);
        await fixture.WaitForProcessedAsync(equivalent.MessageId);
        ReserveStockV1 marker = fixture.Command(100m);
        await fixture.SendAsync(marker);
        StockReservationOutcomeV1 next = await fixture.ReadOutcomeAsync();
        Assert.Equal(marker.OperationId, next.OperationId);
        Assert.Equal(StockReservationOutcome.Shortage, next.Outcome);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
    }

    [Theory]
    [InlineData("organization")]
    [InlineData("unit")]
    public async Task ReserveStock_InvalidReference_RejectsWithoutChangingStock(string mismatch)
    {
        await using var fixture = await ReservationFixture.StartAsync();
        ReserveStockV1 command = fixture.Command(4m);
        command =
            mismatch == "organization"
                ? command with
                {
                    OrganizationId = Guid.CreateVersion7(),
                }
                : command with
                {
                    BaseUnitCode = "KG",
                };
        await fixture.SendAsync(command);
        StockReservationOutcomeV1 rejected = await fixture.ReadOutcomeAsync();
        Assert.Equal(StockReservationOutcome.Rejected, rejected.Outcome);
        Assert.Equal(
            mismatch == "organization" ? "reference-unavailable" : "unit-mismatch",
            rejected.ReasonCode
        );
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(2, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task ReserveStock_ConcurrentSameOperation_CommitsOnlyOneReservation()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        ReserveStockV1 command = fixture.Command(4m);
        ReserveStockV1 repeated = command with { MessageId = Guid.CreateVersion7() };
        await Task.WhenAll(fixture.SendAsync(command), fixture.SendAsync(repeated));
        await fixture.ReadOutcomeAsync();
        await fixture.WaitForProcessedAsync(command.MessageId);
        await fixture.WaitForProcessedAsync(repeated.MessageId);
        ReserveStockV1 marker = fixture.Command(100m);
        await fixture.SendAsync(marker);
        Assert.Equal(marker.OperationId, (await fixture.ReadOutcomeAsync()).OperationId);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task ReserveStock_MalformedCommand_RoutesToInventoryErrorQueueWithoutEffect()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        await fixture.StartErrorObserverAsync();
        ReserveStockV1 command = fixture.Command(-4m);
        await fixture.SendAsync(command);
        Assert.Equal(command, await fixture.ReadErrorAsync());
        Assert.False(fixture.TryReadOutcome(out _));
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(2, (await fixture.StockAsync()).Version);
    }

    [Theory]
    [InlineData("producer")]
    [InlineData("message-id")]
    public async Task ReserveStock_InvalidTransportMetadata_IsPoisonWithoutStockEffect(
        string mismatch
    )
    {
        await using var fixture = await ReservationFixture.StartAsync();
        await fixture.StartErrorObserverAsync();
        ReserveStockV1 command = fixture.Command(4m);
        await fixture.SendAsync(
            command,
            producer: mismatch == "producer" ? "purchasing" : "sales",
            transportMessageId: mismatch == "message-id" ? Guid.CreateVersion7() : null
        );
        Assert.Equal(command, await fixture.ReadErrorAsync());
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(2, (await fixture.StockAsync()).Version);
        Assert.False(fixture.TryReadOutcome(out _));
    }

    [Fact]
    public async Task ReserveStock_ReusedMessageIdentityWithAlteredContent_IsPoisonWithoutSecondEffect()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        await fixture.StartErrorObserverAsync();
        ReserveStockV1 command = fixture.Command(4m);
        await fixture.SendAsync(command);
        await fixture.ReadOutcomeAsync();
        ReserveStockV1 altered = command with { Quantity = 5m };
        await fixture.SendAsync(altered);
        Assert.Equal(altered, await fixture.ReadErrorAsync());
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
        ReserveStockV1 marker = fixture.Command(100m);
        await fixture.SendAsync(marker);
        Assert.Equal(marker.OperationId, (await fixture.ReadOutcomeAsync()).OperationId);
    }

    [Theory]
    [InlineData("inbox_receipts")]
    [InlineData("reservation_operations")]
    [InlineData("events")]
    [InlineData("stock_position_current")]
    [InlineData("audit_entries")]
    [InlineData("outbox_messages")]
    public async Task ReserveStock_AtomicParticipantFails_RollsBackAndCanRetryAfterRepair(
        string table
    )
    {
        await using var fixture = await ReservationFixture.StartAsync();
        await fixture.StartErrorObserverAsync();
        await fixture.ExecuteSqlAsync(
            $$"""
            CREATE FUNCTION inventory.reject_reservation_write() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test reservation atomicity fault'; END $$;
            CREATE TRIGGER reject_reservation_write BEFORE INSERT OR UPDATE ON inventory.{{table}}
            FOR EACH ROW EXECUTE FUNCTION inventory.reject_reservation_write();
            """
        );
        ReserveStockV1 command = fixture.Command(4m);
        await fixture.SendAsync(command);
        ReserveStockV1 poisoned = await fixture.ReadErrorAsync();
        Assert.Equal(command.MessageId, poisoned.MessageId);
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(2, (await fixture.StockAsync()).Version);
        Assert.False(fixture.TryReadOutcome(out _));
        await fixture.ExecuteSqlAsync(
            $"DROP TRIGGER reject_reservation_write ON inventory.{table}; DROP FUNCTION inventory.reject_reservation_write();"
        );
        await fixture.SendAsync(command);
        Assert.Equal(StockReservationOutcome.Reserved, (await fixture.ReadOutcomeAsync()).Outcome);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task ReserveStock_PublishSucceedsButDispatchMarkFails_RetriesSameOutgoingIdentity()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        await fixture.ExecuteSqlAsync(
            """
            CREATE FUNCTION inventory.reject_dispatch_mark() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.dispatched_at IS NOT NULL THEN RAISE EXCEPTION 'test dispatch mark fault'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER reject_dispatch_mark BEFORE UPDATE ON inventory.outbox_messages
            FOR EACH ROW EXECUTE FUNCTION inventory.reject_dispatch_mark();
            """
        );
        await fixture.SendAsync(fixture.Command(4m));
        StockReservationOutcomeV1 first = await fixture.ReadOutcomeAsync();
        StockReservationOutcomeV1 repeated = await fixture.ReadOutcomeAsync();
        Assert.Equal(first, repeated);
        await fixture.ExecuteSqlAsync(
            "DROP TRIGGER reject_dispatch_mark ON inventory.outbox_messages; DROP FUNCTION inventory.reject_dispatch_mark();"
        );
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task ReserveStock_RestartWithExpiredRelayLease_RecoversCommittedOutcome()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        await fixture.ExecuteSqlAsync(
            """
            CREATE FUNCTION inventory.stage_abandoned_lease() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN NEW.lease_token = '11111111-1111-1111-1111-111111111111';
                NEW.lease_until = clock_timestamp() + interval '3 seconds'; RETURN NEW; END $$;
            CREATE TRIGGER stage_abandoned_lease BEFORE INSERT ON inventory.outbox_messages
            FOR EACH ROW EXECUTE FUNCTION inventory.stage_abandoned_lease();
            """
        );
        ReserveStockV1 command = fixture.Command(4m);
        await fixture.SendAsync(command);
        await fixture.WaitForProcessedAsync(command.MessageId);
        Assert.False(fixture.TryReadOutcome(out _));
        await fixture.RestartReceiverAsync();
        StockReservationOutcomeV1 recovered = await fixture.ReadOutcomeAsync();
        Assert.Equal(command.OperationId, recovered.OperationId);
        Assert.Equal(StockReservationOutcome.Reserved, recovered.Outcome);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }
}
