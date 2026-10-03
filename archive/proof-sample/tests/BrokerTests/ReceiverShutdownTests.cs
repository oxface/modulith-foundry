using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class ReceiverShutdownTests
{
    [Fact]
    public async Task Reservation_ShutdownAfterCommitBeforeAck_RedeliversWithoutRepeatingEffect()
    {
        await using var fixture = await ReservationFixture.StartAsync(startReceiver: false);
        var command = fixture.Command(4m);
        await using var receiver = await ReceiverProcess.StartAsync(
            fixture,
            pauseAfterCommit: true
        );
        await fixture.SendAsync(command);
        await receiver.WaitForSignalAsync(
            $"committed:{command.MessageId}",
            fixture.CancellationToken
        );
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        await receiver.StopAsync(fixture.CancellationToken);
        await using var survivor = await ReceiverProcess.StartAsync(fixture);
        await survivor.WaitForSignalAsync(
            $"handled:{command.MessageId}",
            fixture.CancellationToken
        );
        Assert.Equal(StockReservationOutcome.Reserved, (await fixture.ReadOutcomeAsync()).Outcome);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
        Assert.Single(
            (await fixture.HistoryAsync()).Entries,
            entry => entry.Action == StockPositionHistoryAction.Reserved
        );
    }
}
