using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class ReceiverReplicaTests
{
    [Fact]
    public async Task ReleaseReservation_ReplicaDiesWithCommittedDelivery_SurvivorPreservesOneStockRelease()
    {
        await using var fixture = await ReservationFixture.StartAsync();
        var reserve = fixture.Command(4m);
        await fixture.SendAsync(reserve);
        var release = ReservationFixture.ReleaseCommand(reserve, await fixture.ReadOutcomeAsync());
        await fixture.StopReceiverAsync();
        await using var first = await ReceiverProcess.StartAsync(fixture, pauseAfterCommit: true);
        await fixture.SendAsync(release);
        await first.WaitForSignalAsync($"committed:{release.MessageId}", fixture.CancellationToken);
        var released = await fixture.ReadReleaseOutcomeAsync();
        Assert.Equal(StockReservationReleaseOutcome.Released, released.Outcome);
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);

        await using var survivor = await ReceiverProcess.StartAsync(fixture);
        var duplicate = release with { MessageId = Guid.CreateVersion7() };
        for (int delivery = 0; delivery < 8; delivery++)
            await fixture.SendAsync(duplicate);
        await survivor.WaitForSignalAsync(
            $"handled:{duplicate.MessageId}",
            fixture.CancellationToken
        );
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(4, (await fixture.StockAsync()).Version);

        await first.KillAsync();
        await survivor.WaitForSignalAsync(
            $"handled:{release.MessageId}",
            fixture.CancellationToken
        );
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(4, (await fixture.StockAsync()).Version);
        Assert.Single(
            (await fixture.HistoryAsync()).Entries,
            entry => entry.Action == StockPositionHistoryAction.ReservationReleased
        );
    }

    [Fact]
    public async Task CreateRequirement_ReplicaDiesWithCommittedDelivery_SurvivorRetainsOneRequirement()
    {
        await using var fixture = await StockItemBootstrapFixture.StartAsync();
        var item = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        await fixture.StartProducerOnlyAsync();
        await fixture.StartCreatedObserverAsync();
        var command = new CreateReplenishmentRequirementV1(
            Guid.CreateVersion7(),
            fixture.Actor.OrganizationId.Value,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            1,
            item.Value,
            4m,
            "EA",
            DateTimeOffset.UtcNow
        );
        await using var first = await ReceiverProcess.StartPurchasingAsync(
            fixture,
            pauseAfterCommit: true
        );
        await fixture.SendAsync(command);
        await first.WaitForSignalAsync($"committed:{command.MessageId}", fixture.CancellationToken);
        var created = await fixture.ReadCreatedAsync();
        var original = Assert
            .IsType<GetReplenishmentRequirementResult.Found>(
                await fixture.RequirementAsync(created.RequirementNumber)
            )
            .Requirement;
        Assert.Equal(command.OperationId, created.OperationId);
        Assert.Equal(4m, original.Quantity);

        await using var survivor = await ReceiverProcess.StartPurchasingAsync(fixture);
        var duplicate = command with { MessageId = Guid.CreateVersion7() };
        for (int delivery = 0; delivery < 8; delivery++)
            await fixture.SendAsync(duplicate);
        await survivor.WaitForSignalAsync(
            $"handled:{duplicate.MessageId}",
            fixture.CancellationToken
        );
        Assert.Equal(
            created.RequirementId,
            (await fixture.RequestAsync(command.OperationId))!.RequirementId
        );

        await first.KillAsync();
        await survivor.WaitForSignalAsync(
            $"handled:{command.MessageId}",
            fixture.CancellationToken
        );
        var requirement = Assert.Single(
            Assert
                .IsType<ListReplenishmentRequirementsResult.Listed>(
                    await fixture.RequirementsAsync()
                )
                .Requirements
        );
        Assert.Equal(original, requirement);
        Assert.Equal("completed", (await fixture.RequestAsync(command.OperationId))!.Status);
    }

    [Fact]
    public async Task ReleaseOutcome_ReplicaDiesWithCommittedDelivery_SurvivorPreservesOneCompletion()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var (order, release) = await fixture.PrepareControlledCancellationAsync();
        await fixture.StopAsync();
        var outcome = SalesCompensationOutcomeTests.Released(release);
        await using var first = await ReceiverProcess.StartSalesAsync(
            fixture,
            pauseAfterCommit: true
        );
        await fixture.PublishAsync(outcome);
        await first.WaitForSignalAsync($"committed:{outcome.MessageId}", fixture.CancellationToken);
        var completed = await fixture.ReadAsync(order.OrderNumber);
        Assert.Equal(OrderFulfilmentStatus.Compensated, completed.Status);

        await using var survivor = await ReceiverProcess.StartSalesAsync(fixture);
        var duplicate = outcome with { MessageId = Guid.CreateVersion7() };
        for (int delivery = 0; delivery < 8; delivery++)
            await fixture.PublishAsync(duplicate);
        await survivor.WaitForSignalAsync(
            $"handled:{duplicate.MessageId}",
            fixture.CancellationToken
        );
        Assert.Equal(completed.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);

        await first.KillAsync();
        await survivor.WaitForSignalAsync(
            $"handled:{outcome.MessageId}",
            fixture.CancellationToken
        );
        var retained = await fixture.ReadAsync(order.OrderNumber);
        Assert.Equal(completed.Version, retained.Version);
        Assert.Equal(OrderFulfilmentStatus.Compensated, retained.Status);
        var activity = await fixture.ActivityAsync(order.OrderNumber);
        Assert.Single(activity, item => item.Kind == SalesOrderActivityKind.ReservationReleased);
        Assert.Single(activity, item => item.Kind == SalesOrderActivityKind.CompensationCompleted);
    }

    [Fact]
    public async Task ReserveStock_ReplicaDiesWithCommittedDelivery_SurvivorSettlesWithoutSecondStockEffect()
    {
        await using var fixture = await ReservationFixture.StartAsync(startReceiver: false);
        var command = fixture.Command(4m);
        await using var first = await ReceiverProcess.StartAsync(fixture, pauseAfterCommit: true);
        await fixture.SendAsync(command);
        await first.WaitForSignalAsync($"committed:{command.MessageId}", fixture.CancellationToken);
        var reserved = await fixture.ReadOutcomeAsync();
        Assert.Equal(StockReservationOutcome.Reserved, reserved.Outcome);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);

        await using var survivor = await ReceiverProcess.StartAsync(fixture);
        var duplicate = command with { MessageId = Guid.CreateVersion7() };
        // A bounded burst exceeds the paused replica's prefetch. Do not rely on fair delivery
        // or assert an exact per-replica distribution; require the survivor to handle work.
        for (int delivery = 0; delivery < 8; delivery++)
            await fixture.SendAsync(duplicate);
        await survivor.WaitForSignalAsync(
            $"handled:{duplicate.MessageId}",
            fixture.CancellationToken
        );
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);

        await first.KillAsync();
        await survivor.WaitForSignalAsync(
            $"handled:{command.MessageId}",
            fixture.CancellationToken
        );
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
        Assert.Single(
            (await fixture.HistoryAsync()).Entries,
            entry => entry.Action == StockPositionHistoryAction.Reserved
        );
    }
}
