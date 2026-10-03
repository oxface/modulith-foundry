using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class SalesCompensationCrashTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleaseOutcome_AbruptExitAroundCommit_RedeliversWithOneCompletion(
        bool afterCommit
    )
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var (order, release) = await fixture.PrepareControlledCancellationAsync();
        var outcome = SalesCompensationOutcomeTests.Released(release);
        await fixture.StopAsync();
        if (afterCommit)
        {
            await using var child = await ReceiverProcess.StartSalesAsync(
                fixture,
                pauseAfterCommit: true
            );
            await fixture.PublishAsync(outcome);
            await child.WaitForSignalAsync(
                $"committed:{outcome.MessageId}",
                fixture.CancellationToken
            );
            Assert.Equal(
                OrderFulfilmentStatus.Compensated,
                (await fixture.ReadAsync(order.OrderNumber)).Status
            );
            await child.KillAsync();
        }
        else
        {
            await using var barrier = await ReceiverDatabaseBarrier.CreateSalesAsync(fixture);
            await using var child = await ReceiverProcess.StartSalesAsync(fixture);
            await fixture.PublishAsync(outcome);
            await barrier.WaitAsync(child);
            Assert.Equal(
                OrderFulfilmentStatus.CompensationPending,
                (await fixture.ReadAsync(order.OrderNumber)).Status
            );
            await child.KillAsync();
            await barrier.WaitForDisconnectAsync(child);
        }
        await using var restarted = await ReceiverProcess.StartSalesAsync(fixture);
        await restarted.WaitForSignalAsync(
            $"handled:{outcome.MessageId}",
            fixture.CancellationToken
        );
        Assert.Equal(
            OrderFulfilmentStatus.Compensated,
            (await fixture.ReadAsync(order.OrderNumber)).Status
        );
        Assert.Single(
            await fixture.ActivityAsync(order.OrderNumber),
            item => item.Kind == SalesOrderActivityKind.ReservationReleased
        );
        Assert.Single(
            await fixture.ActivityAsync(order.OrderNumber),
            item => item.Kind == SalesOrderActivityKind.CompensationCompleted
        );
    }
}
