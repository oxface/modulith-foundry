using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class InventoryCrashRecoveryTests
{
    [Fact]
    public async Task ReserveStock_AbruptExitBeforeCommit_RollsBackAndRedelivers()
    {
        await using var fixture = await ReservationFixture.StartAsync(startReceiver: false);
        ReserveStockV1 command = fixture.Command(4m);
        await using (
            ReceiverDatabaseBarrier barrier = await ReceiverDatabaseBarrier.CreateAsync(
                fixture,
                dispatchMark: false
            )
        )
        {
            await using ReceiverProcess child = await ReceiverProcess.StartAsync(fixture);
            await fixture.SendAsync(command);
            await barrier.WaitAsync(child);
            Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
            Assert.Equal(2, (await fixture.StockAsync()).Version);
            await child.KillAsync();
            await barrier.WaitForDisconnectAsync(child);
        }

        await using ReceiverProcess restarted = await ReceiverProcess.StartAsync(fixture);
        await restarted.WaitForSignalAsync(
            $"handled:{command.MessageId}",
            fixture.CancellationToken
        );
        Assert.Equal(StockReservationOutcome.Reserved, (await fixture.ReadOutcomeAsync()).Outcome);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task ReserveStock_AbruptExitAfterCommit_RedeliversWithoutSecondStockEffect()
    {
        await using var fixture = await ReservationFixture.StartAsync(startReceiver: false);
        ReserveStockV1 command = fixture.Command(4m);
        await using (
            ReceiverProcess child = await ReceiverProcess.StartAsync(
                fixture,
                pauseAfterCommit: true
            )
        )
        {
            await fixture.SendAsync(command);
            await child.WaitForSignalAsync(
                $"committed:{command.MessageId}",
                fixture.CancellationToken
            );
            Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
            await child.KillAsync();
        }

        await using ReceiverProcess restarted = await ReceiverProcess.StartAsync(fixture);
        await restarted.WaitForSignalAsync(
            $"handled:{command.MessageId}",
            fixture.CancellationToken
        );
        StockReservationOutcomeV1 outcome = await fixture.ReadOutcomeAsync();
        Assert.Equal(command.OperationId, outcome.OperationId);
        Assert.Equal(StockReservationOutcome.Reserved, outcome.Outcome);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task PublishOutcome_AbruptExitBeforeDispatchMark_RepeatsStableIdentityAfterLeaseExpiry()
    {
        await using var fixture = await ReservationFixture.StartAsync(startReceiver: false);
        ReserveStockV1 command = fixture.Command(4m);
        StockReservationOutcomeV1 first;
        await using (
            ReceiverDatabaseBarrier barrier = await ReceiverDatabaseBarrier.CreateAsync(
                fixture,
                dispatchMark: true
            )
        )
        {
            await using ReceiverProcess child = await ReceiverProcess.StartAsync(fixture);
            await fixture.SendAsync(command);
            first = await fixture.ReadOutcomeAsync();
            await barrier.WaitAsync(child);
            await child.KillAsync();
            await barrier.WaitForDisconnectAsync(child);
        }

        await using ReceiverProcess restarted = await ReceiverProcess.StartAsync(fixture);
        // No lease rewrites or accelerated clock: wait for the production 30-second lease.
        StockReservationOutcomeV1 repeated = await fixture.ReadOutcomeAsync();
        Assert.Equal(first, repeated);
        Assert.Equal(command.OperationId, repeated.OperationId);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }
}
