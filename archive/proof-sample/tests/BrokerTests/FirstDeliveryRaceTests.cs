using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class FirstDeliveryRaceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Requirement_CompetingFirstDeliveries_SerializeOneCreation(bool sameMessageId)
    {
        await using var fixture = await StockItemBootstrapFixture.StartAsync();
        using var broker = fixture.ObserveBroker();
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
        await using var barrier = await ReceiverDatabaseBarrier.CreateRequirementAsync(fixture);
        await using var first = await ReceiverProcess.StartPurchasingAsync(fixture);
        await fixture.SendAsync(command);
        await barrier.WaitAsync(first);
        await using var second = await ReceiverProcess.StartPurchasingAsync(fixture);
        var competing = sameMessageId
            ? command
            : command with
            {
                MessageId = Guid.CreateVersion7(),
            };
        for (int delivery = 0; delivery < 8; delivery++)
            await fixture.SendAsync(competing);
        await second.WaitForSignalAsync(
            $"dispatch:{competing.MessageId}",
            fixture.CancellationToken
        );
        await barrier.WaitForRowLockAsync(second);
        Assert.Null(await fixture.RequestAsync(command.OperationId));
        await barrier.ReleaseAsync();
        await WaitForSettlementAsync(
            first,
            second,
            command.MessageId,
            competing.MessageId,
            broker,
            ModulithFoundry.Modules.Purchasing.Composition.PurchasingMessaging.InputQueue,
            ModulithFoundry.Modules.Purchasing.Composition.PurchasingMessaging.ErrorQueue,
            fixture.CancellationToken
        );
        var created = await fixture.ReadCreatedAsync();
        Assert.Equal(command.OperationId, created.OperationId);
        Assert.Equal("completed", (await fixture.RequestAsync(command.OperationId))!.Status);
        Assert.Equal(
            created.RequirementId,
            (await fixture.RequestAsync(command.OperationId))!.RequirementId
        );
        var requirement = Assert.Single(
            Assert
                .IsType<ListReplenishmentRequirementsResult.Listed>(
                    await fixture.RequirementsAsync()
                )
                .Requirements
        );
        Assert.Equal(created.RequirementId, requirement.RequirementId);
        Assert.Equal(4m, requirement.Quantity);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReleaseOutcome_CompetingFirstDeliveries_CommitOneCompletion(
        bool sameMessageId
    )
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        using var broker = fixture.ObserveBroker();
        var (order, release) = await fixture.PrepareControlledCancellationAsync();
        await fixture.StopAsync();
        var outcome = SalesCompensationOutcomeTests.Released(release);
        await using var first = await ReceiverProcess.StartSalesAsync(
            fixture,
            pauseReceiptRead: true
        );
        await fixture.PublishAsync(outcome);
        await first.WaitForSignalAsync($"lookup:{outcome.MessageId}", fixture.CancellationToken);
        await using var second = await ReceiverProcess.StartSalesAsync(
            fixture,
            pauseReceiptRead: true
        );
        var competing = sameMessageId
            ? outcome
            : outcome with
            {
                MessageId = Guid.CreateVersion7(),
            };
        for (int delivery = 0; delivery < 8; delivery++)
            await fixture.PublishAsync(competing);
        await second.WaitForSignalAsync($"lookup:{competing.MessageId}", fixture.CancellationToken);
        Assert.Equal(
            OrderFulfilmentStatus.CompensationPending,
            (await fixture.ReadAsync(order.OrderNumber)).Status
        );
        await first.ReleaseReceiptReadAsync(fixture.CancellationToken);
        await second.ReleaseReceiptReadAsync(fixture.CancellationToken);
        await WaitForSettlementAsync(
            first,
            second,
            outcome.MessageId,
            competing.MessageId,
            broker,
            ModulithFoundry.Modules.Sales.Composition.SalesMessaging.InputQueue,
            ModulithFoundry.Modules.Sales.Composition.SalesMessaging.ErrorQueue,
            fixture.CancellationToken
        );
        Assert.Equal(
            OrderFulfilmentStatus.Compensated,
            (await fixture.ReadAsync(order.OrderNumber)).Status
        );
        Assert.Single(
            await fixture.ActivityAsync(order.OrderNumber),
            entry => entry.Kind == SalesOrderActivityKind.ReservationReleased
        );
        Assert.Single(
            await fixture.ActivityAsync(order.OrderNumber),
            entry => entry.Kind == SalesOrderActivityKind.CompensationCompleted
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reservation_CompetingFirstDeliveries_CommitOneEffect(bool sameMessageId)
    {
        await using var fixture = await ReservationFixture.StartAsync(startReceiver: false);
        using var broker = fixture.ObserveBroker();
        var command = fixture.Command(4m);
        await using var first = await ReceiverProcess.StartAsync(fixture, pauseReceiptRead: true);
        await fixture.SendAsync(command);
        await first.WaitForSignalAsync($"lookup:{command.MessageId}", fixture.CancellationToken);
        await using var second = await ReceiverProcess.StartAsync(fixture, pauseReceiptRead: true);
        var competing = sameMessageId
            ? command
            : command with
            {
                MessageId = Guid.CreateVersion7(),
            };
        // Exceed the first endpoint's prefetch; require a real second-process read, not assumed fairness.
        for (int delivery = 0; delivery < 8; delivery++)
            await fixture.SendAsync(competing);
        await second.WaitForSignalAsync($"lookup:{competing.MessageId}", fixture.CancellationToken);
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(2, (await fixture.StockAsync()).Version);
        await first.ReleaseReceiptReadAsync(fixture.CancellationToken);
        await second.ReleaseReceiptReadAsync(fixture.CancellationToken);
        await WaitForSettlementAsync(
            first,
            second,
            command.MessageId,
            competing.MessageId,
            broker,
            ModulithFoundry.Modules.Inventory.Composition.InventoryMessaging.InputQueue,
            ModulithFoundry.Modules.Inventory.Composition.InventoryMessaging.ErrorQueue,
            fixture.CancellationToken
        );
        var outcome = await fixture.ReadOutcomeAsync();
        Assert.Equal(command.OperationId, outcome.OperationId);
        Assert.Equal(StockReservationOutcome.Reserved, outcome.Outcome);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
        Assert.Single(
            (await fixture.HistoryAsync()).Entries,
            entry => entry.Action == StockPositionHistoryAction.Reserved
        );
    }

    private static async Task WaitForSettlementAsync(
        ReceiverProcess first,
        ReceiverProcess second,
        Guid original,
        Guid competing,
        BrokerQueueProbe broker,
        string inputQueue,
        string errorQueue,
        CancellationToken cancellationToken
    )
    {
        int Observed(Guid id) =>
            first.ObservedSignalCount($"handled:{id}")
            + second.ObservedSignalCount($"handled:{id}");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        // NACK can move either identity to either receiver; it does not reserve receiver ownership.
        int Total() =>
            original == competing ? Observed(original) : Observed(original) + Observed(competing);
        // All nine physical deliveries must complete, not only a winner while losers become poison.
        while (Observed(original) < 1 || Observed(competing) < 1 || Total() < 9)
            await Task.Delay(50, timeout.Token);
        await broker.WaitAsync(
            inputQueue,
            state => state.Ready == 0 && state.Unacknowledged == 0,
            timeout.Token
        );
        await broker.WaitAsync(
            errorQueue,
            state => state.Ready == 0 && state.Unacknowledged == 0,
            timeout.Token,
            missingIsEmpty: true
        );
    }
}
