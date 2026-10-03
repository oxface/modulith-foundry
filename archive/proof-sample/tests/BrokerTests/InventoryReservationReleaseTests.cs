using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class InventoryReservationReleaseTests
{
    [Theory]
    [InlineData("producer")]
    [InlineData("message-id")]
    [InlineData("correlation-id")]
    [InlineData("payload")]
    public async Task ReleaseReservation_InvalidEnvelopeOrIdentifiers_GoesToOwnErrorQueueWithoutStockEffect(
        string invalid
    )
    {
        await using var fixture = await ReservationFixture.StartAsync();
        var reserve = fixture.Command(4m);
        await fixture.SendAsync(reserve);
        var release = ReservationFixture.ReleaseCommand(reserve, await fixture.ReadOutcomeAsync());
        if (invalid == "payload")
            release = release with { OperationId = Guid.Empty };
        await fixture.StartErrorObserverAsync();
        await fixture.SendAsync(
            release,
            producer: invalid == "producer" ? "purchasing" : "sales",
            transportMessageId: invalid == "message-id" ? Guid.CreateVersion7() : null,
            correlationId: invalid == "correlation-id" ? Guid.CreateVersion7() : null
        );
        Assert.Equal(release, await fixture.ReadReleaseErrorAsync());
        Assert.False(fixture.TryReadReleaseOutcome(out _));
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleaseReservation_AbruptExitAroundCommit_RedeliversWithOneStockEffect(
        bool afterCommit
    )
    {
        await using var fixture = await ReservationFixture.StartAsync();
        var reserve = fixture.Command(4m);
        await fixture.SendAsync(reserve);
        var release = ReservationFixture.ReleaseCommand(reserve, await fixture.ReadOutcomeAsync());
        await fixture.StopReceiverAsync();
        if (afterCommit)
        {
            await using var child = await ReceiverProcess.StartAsync(
                fixture,
                pauseAfterCommit: true
            );
            await fixture.SendAsync(release);
            await child.WaitForSignalAsync(
                $"committed:{release.MessageId}",
                fixture.CancellationToken
            );
            Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
            await child.KillAsync();
        }
        else
        {
            await using var barrier = await ReceiverDatabaseBarrier.CreateAsync(
                fixture,
                dispatchMark: false
            );
            await using var child = await ReceiverProcess.StartAsync(fixture);
            await fixture.SendAsync(release);
            await barrier.WaitAsync(child);
            Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
            await child.KillAsync();
            await barrier.WaitForDisconnectAsync(child);
        }
        await using var restarted = await ReceiverProcess.StartAsync(fixture);
        await restarted.WaitForSignalAsync(
            $"handled:{release.MessageId}",
            fixture.CancellationToken
        );
        Assert.Equal(
            StockReservationReleaseOutcome.Released,
            (await fixture.ReadReleaseOutcomeAsync()).Outcome
        );
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(4, (await fixture.StockAsync()).Version);
        Assert.Single(
            (await fixture.HistoryAsync()).Entries,
            entry => entry.Action == StockPositionHistoryAction.ReservationReleased
        );
    }

    [Fact]
    public async Task ReleaseOutcome_AbruptExitBeforeDispatchMark_RepeatsStableIdentityAfterLeaseExpiry()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        var reserve = fixture.Command(4m);
        await fixture.SendAsync(reserve);
        var release = ReservationFixture.ReleaseCommand(reserve, await fixture.ReadOutcomeAsync());
        await fixture.StopReceiverAsync();
        StockReservationReleaseOutcomeV1 first;
        await using (
            var barrier = await ReceiverDatabaseBarrier.CreateReleaseDispatchAsync(fixture)
        )
        {
            await using var child = await ReceiverProcess.StartAsync(fixture);
            await fixture.SendAsync(release);
            first = await fixture.ReadReleaseOutcomeAsync();
            await barrier.WaitAsync(child);
            await child.KillAsync();
            await barrier.WaitForDisconnectAsync(child);
        }
        await using var restarted = await ReceiverProcess.StartAsync(fixture);
        Assert.Equal(first, await fixture.ReadReleaseOutcomeAsync());
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(4, (await fixture.StockAsync()).Version);
    }

    [Theory]
    [InlineData("inbox_receipts")]
    [InlineData("reservation_release_operations")]
    [InlineData("event_streams")]
    [InlineData("events")]
    [InlineData("stock_position_current")]
    [InlineData("audit_entries")]
    [InlineData("outbox_messages")]
    public async Task ReleaseReservation_AtomicParticipantFails_RollsBackAndCanRetryAfterRepair(
        string table
    )
    {
        await using var fixture = await ReservationFixture.StartAsync();
        var reserve = fixture.Command(4m);
        await fixture.SendAsync(reserve);
        var release = ReservationFixture.ReleaseCommand(reserve, await fixture.ReadOutcomeAsync());
        var before = await fixture.StockAsync();
        await fixture.StartErrorObserverAsync();
        await fixture.ExecuteSqlAsync(
            $$"""
            CREATE FUNCTION inventory.reject_release_write() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test release atomicity fault'; END $$;
            CREATE TRIGGER reject_release_write BEFORE INSERT OR UPDATE ON inventory.{{table}}
            FOR EACH ROW EXECUTE FUNCTION inventory.reject_release_write();
            """
        );
        await fixture.SendAsync(release);
        Assert.Equal(release, await fixture.ReadReleaseErrorAsync());
        Assert.Equal(before, await fixture.StockAsync());
        Assert.DoesNotContain(
            (await fixture.HistoryAsync()).Entries,
            entry => entry.Action == StockPositionHistoryAction.ReservationReleased
        );
        Assert.False(fixture.TryReadReleaseOutcome(out _));
        await fixture.ExecuteSqlAsync(
            $"DROP TRIGGER reject_release_write ON inventory.{table}; DROP FUNCTION inventory.reject_release_write();"
        );
        await fixture.SendAsync(release);
        Assert.Equal(
            StockReservationReleaseOutcome.Released,
            (await fixture.ReadReleaseOutcomeAsync()).Outcome
        );
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(4, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task ReleaseReservation_CompetingOperations_AppendOnlyOneRelease()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        var reserve = fixture.Command(4m);
        await fixture.SendAsync(reserve);
        var first = ReservationFixture.ReleaseCommand(reserve, await fixture.ReadOutcomeAsync());
        var second = first with
        {
            MessageId = Guid.CreateVersion7(),
            OperationId = Guid.CreateVersion7(),
        };
        await Task.WhenAll(fixture.SendAsync(first), fixture.SendAsync(second));
        var outcomes = new[]
        {
            await fixture.ReadReleaseOutcomeAsync(),
            await fixture.ReadReleaseOutcomeAsync(),
        };
        Assert.Single(
            outcomes,
            outcome => outcome.Outcome == StockReservationReleaseOutcome.Released
        );
        Assert.Single(
            outcomes,
            outcome => outcome.Outcome == StockReservationReleaseOutcome.AlreadyReleased
        );
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(4, (await fixture.StockAsync()).Version);
        Assert.Single(
            (await fixture.HistoryAsync()).Entries,
            entry => entry.Action == StockPositionHistoryAction.ReservationReleased
        );
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("process")]
    [InlineData("reservation")]
    public async Task ReleaseReservation_ForeignCorrelation_IsRejectedWithoutChangingStock(
        string mismatch
    )
    {
        await using var fixture = await ReservationFixture.StartAsync();
        var reserve = fixture.Command(4m);
        await fixture.SendAsync(reserve);
        var release = ReservationFixture.ReleaseCommand(reserve, await fixture.ReadOutcomeAsync());
        release = mismatch switch
        {
            "tenant" => release with { OrganizationId = Guid.CreateVersion7() },
            "process" => release with { ProcessId = Guid.CreateVersion7() },
            "reservation" => release with { ReservationId = Guid.CreateVersion7() },
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
        };
        await fixture.SendAsync(release);
        var denied = await fixture.ReadReleaseOutcomeAsync();
        Assert.Equal(StockReservationReleaseOutcome.Rejected, denied.Outcome);
        Assert.Equal(
            mismatch == "tenant" ? "reservation-unavailable" : "reservation-correlation-mismatch",
            denied.ReasonCode
        );
        Assert.Null(denied.ReservationQuantity);
        Assert.Null(denied.BaseUnitCode);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("released-flag")]
    public async Task ReleaseReservation_DamagedWriteModel_RequiresRepairBeforeRedrive(
        string damage
    )
    {
        await using var fixture = await ReservationFixture.StartAsync();
        var reserve = fixture.Command(4m);
        await fixture.SendAsync(reserve);
        var release = ReservationFixture.ReleaseCommand(reserve, await fixture.ReadOutcomeAsync());
        var before = await fixture.StockAsync();
        await fixture.StartErrorObserverAsync();
        await fixture.ExecuteSqlAsync(
            damage == "missing"
                ? "DELETE FROM inventory.stock_position_current;"
                : "UPDATE inventory.stock_position_current SET reservations = jsonb_set(reservations, '{0,IsReleased}', 'true'::jsonb);"
        );
        await fixture.SendAsync(release);
        Assert.Equal(release, await fixture.ReadReleaseErrorAsync());
        Assert.False(fixture.TryReadReleaseOutcome(out _));
        await fixture.RebuildAsync(before.StockPositionId, previousModelMatched: false);
        Assert.Equal(before, await fixture.StockAsync());
        Assert.Equal(4m, (await fixture.StockAtVersionAsync(3)).ReservedQuantity);
        await fixture.SendAsync(release);
        Assert.Equal(
            StockReservationReleaseOutcome.Released,
            (await fixture.ReadReleaseOutcomeAsync()).Outcome
        );
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(4, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task ReleaseReservation_InactiveReferencesAndLegacyWriteState_ReleasesAndPreservesHistory()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        var reserve = fixture.Command(4m);
        await fixture.SendAsync(reserve);
        var release = ReservationFixture.ReleaseCommand(reserve, await fixture.ReadOutcomeAsync());
        await fixture.DeactivateReferencesAsync();
        // Arrange the pre-release JSON shape; the new optional flag defaults to active.
        await fixture.ExecuteSqlAsync(
            """
            UPDATE inventory.stock_position_current SET reservations = (
                SELECT jsonb_agg(entry - 'IsReleased') FROM jsonb_array_elements(reservations) entry
            );
            """
        );
        await fixture.SendAsync(release);
        Assert.Equal(
            StockReservationReleaseOutcome.Released,
            (await fixture.ReadReleaseOutcomeAsync()).Outcome
        );
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(4m, (await fixture.StockAtVersionAsync(3)).ReservedQuantity);
        var current = await fixture.StockAsync();
        await fixture.RebuildAsync(current.StockPositionId);
        Assert.Equal(current, await fixture.StockAsync());
        var marker = release with
        {
            MessageId = Guid.CreateVersion7(),
            OperationId = Guid.CreateVersion7(),
        };
        await fixture.SendAsync(marker);
        Assert.Equal(marker.OperationId, (await fixture.ReadReleaseOutcomeAsync()).OperationId);
        Assert.Equal(4, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task ReleaseReservation_RepeatedDeliveryAndNewOperation_ReleasesOnlyOnceAfterRebuild()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        var reserve = fixture.Command(4m);
        await fixture.SendAsync(reserve);
        var release = ReservationFixture.ReleaseCommand(reserve, await fixture.ReadOutcomeAsync());
        fixture.FailSettlementOnce(release.MessageId);
        await fixture.SendAsync(release);
        Assert.Equal(
            StockReservationReleaseOutcome.Released,
            (await fixture.ReadReleaseOutcomeAsync()).Outcome
        );
        await fixture.WaitForProcessedAsync(release.MessageId, 2);
        await fixture.SendAsync(release);
        await fixture.WaitForProcessedAsync(release.MessageId, 3);
        var duplicate = release with
        {
            MessageId = Guid.CreateVersion7(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await fixture.SendAsync(duplicate);
        await fixture.WaitForProcessedAsync(duplicate.MessageId);
        var stock = await fixture.StockAsync();
        await fixture.RebuildAsync(stock.StockPositionId);
        var nextOperation = release with
        {
            MessageId = Guid.CreateVersion7(),
            OperationId = Guid.CreateVersion7(),
        };
        await fixture.SendAsync(nextOperation);
        var already = await fixture.ReadReleaseOutcomeAsync();
        Assert.Equal(nextOperation.OperationId, already.OperationId);
        Assert.Equal(StockReservationReleaseOutcome.AlreadyReleased, already.Outcome);
        Assert.Equal(4m, already.ReservationQuantity);
        Assert.Equal(stock, await fixture.StockAsync());
        Assert.Single(
            (await fixture.HistoryAsync()).Entries,
            entry => entry.Action == StockPositionHistoryAction.ReservationReleased
        );
        // A delayed reservation replay must not resurrect the ended commitment.
        var delayed = reserve with
        {
            MessageId = Guid.CreateVersion7(),
        };
        await fixture.SendAsync(delayed);
        await fixture.WaitForProcessedAsync(delayed.MessageId);
        var marker = fixture.Command(100m);
        await fixture.SendAsync(marker);
        Assert.Equal(marker.OperationId, (await fixture.ReadOutcomeAsync()).OperationId);
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
    }

    [Fact]
    public async Task ReleaseReservation_ConflictingIdentity_DoesNotReleaseAnotherReservation()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        var first = fixture.Command(4m);
        await fixture.SendAsync(first);
        var release = ReservationFixture.ReleaseCommand(first, await fixture.ReadOutcomeAsync());
        var second = fixture.Command(2m);
        await fixture.SendAsync(second);
        var secondReservation = await fixture.ReadOutcomeAsync();
        await fixture.SendAsync(release);
        await fixture.ReadReleaseOutcomeAsync();
        var conflicting = release with
        {
            MessageId = Guid.CreateVersion7(),
            ReservationOperationId = second.OperationId,
            ReservationId = secondReservation.ReservationId!.Value,
        };
        await fixture.SendAsync(conflicting);
        var denied = await fixture.ReadReleaseOutcomeAsync();
        Assert.Equal(StockReservationReleaseOutcome.Rejected, denied.Outcome);
        Assert.Equal("operation-conflict", denied.ReasonCode);
        await fixture.StartErrorObserverAsync();
        var conflictingDelivery = release with
        {
            ReservationId = secondReservation.ReservationId!.Value,
        };
        await fixture.SendAsync(conflictingDelivery);
        Assert.Equal(conflictingDelivery, await fixture.ReadReleaseErrorAsync());
        Assert.Equal(2m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(5, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task ReleaseReservation_ExistingReservation_AppendsReleaseAndRestoresAvailability()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        var reserve = fixture.Command(4m);
        await fixture.SendAsync(reserve);
        var reserved = await fixture.ReadOutcomeAsync();
        var release = ReservationFixture.ReleaseCommand(reserve, reserved);
        await fixture.SendAsync(release);
        var outcome = await fixture.ReadReleaseOutcomeAsync();
        Assert.Equal(StockReservationReleaseOutcome.Released, outcome.Outcome);
        Assert.Equal(release.MessageId, outcome.CausationId);
        Assert.Equal(release.OperationId, outcome.OperationId);
        Assert.Equal(reserve.OperationId, outcome.ReservationOperationId);
        Assert.Equal(reserved.ReservationId, outcome.ReservationId);
        Assert.Equal(4m, outcome.ReservationQuantity);
        Assert.Equal("EA", outcome.BaseUnitCode);
        var stock = await fixture.StockAsync();
        Assert.Equal(10m, stock.OnHandQuantity);
        Assert.Equal(0m, stock.ReservedQuantity);
        Assert.Equal(10m, stock.AvailableQuantity);
        Assert.Equal(4, stock.Version);
        var history = await fixture.HistoryAsync();
        Assert.Equal(
            4m,
            Assert
                .Single(
                    history.Entries,
                    entry => entry.Action == StockPositionHistoryAction.ReservationReleased
                )
                .Quantity
        );
    }
}
