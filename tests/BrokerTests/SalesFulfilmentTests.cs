using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class SalesFulfilmentTests
{
    [Fact]
    public async Task ApproveOrder_TwoAvailableLines_ReservesStockThroughDurableRoundTrip()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var order = await fixture.ApproveAsync(2m, 3m);
        var process = await fixture.WaitAsync(order.OrderNumber, "reserved");
        Assert.Equal(4, process.Version);
        Assert.All(
            process.Lines,
            line =>
            {
                Assert.Equal(OrderFulfilmentLineStatus.Reserved, line.Status);
                Assert.NotNull(line.ReservationId);
                Assert.Null(line.ResponseDeadline);
                Assert.Equal(1, line.AttemptCount);
            }
        );
        Assert.Equal(5m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(4, (await fixture.StockAsync()).Version);
        var activity = await fixture.ActivityAsync(order.OrderNumber);
        Assert.Equal(
            new[]
            {
                SalesOrderActivityKind.Created,
                SalesOrderActivityKind.Submitted,
                SalesOrderActivityKind.Approved,
                SalesOrderActivityKind.FulfilmentStarted,
            },
            activity.Take(4).Select(entry => entry.Kind)
        );
        Assert.All(
            activity.Skip(4),
            entry => Assert.Equal(SalesOrderActivityKind.StockReserved, entry.Kind)
        );
    }

    [Fact]
    public async Task ApproveOrder_MixedAvailability_RecordsIndependentOutcomesAndSystemActivity()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var order = await fixture.ApproveAsync(2m, 100m);
        var process = await fixture.WaitAsync(order.OrderNumber, "awaiting-replenishment");
        Assert.Equal(OrderFulfilmentLineStatus.Reserved, process.Lines[0].Status);
        Assert.Equal(OrderFulfilmentLineStatus.Shortage, process.Lines[1].Status);
        Assert.Equal(2m, (await fixture.StockAsync()).ReservedQuantity);
        var activity = await fixture.ActivityAsync(order.OrderNumber);
        Assert.Equal(6, activity.Count);
        Assert.Equal(2, activity.Count(entry => entry.LineNumber.HasValue));
        Assert.All(
            activity.Where(entry => entry.ProcessVersion.HasValue),
            entry =>
            {
                Assert.Null(entry.ActorUserId);
                Assert.Equal("sales.order-fulfilment", entry.SystemActor);
            }
        );
        Assert.Equal(
            SalesOrderStatus.Approved,
            (await fixture.ReadOrderAsync(order.OrderNumber)).Status
        );
    }

    [Fact]
    public async Task ApproveOrder_OutboxInsertFails_RollsBackApprovalAndCanRetry()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var submitted = await fixture.SubmitAsync(2m);
        await fixture.ExecuteSetupAsync(
            """
            CREATE FUNCTION sales.fail_outbox() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test outbox fault'; END $$;
            CREATE TRIGGER fail_outbox BEFORE INSERT ON sales.outbox_messages
            FOR EACH ROW EXECUTE FUNCTION sales.fail_outbox();
            """
        );
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.ApproveAsync(submitted));
        Assert.Equal(
            SalesOrderStatus.AwaitingApproval,
            (await fixture.ReadOrderAsync(submitted.OrderNumber)).Status
        );
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(2, (await fixture.ActivityAsync(submitted.OrderNumber)).Count);
        await fixture.RunAsync(
            fixture.Administrator,
            async services =>
                Assert.IsType<GetOrderFulfilmentResult.NotFound>(
                    await services
                        .GetRequiredService<ISalesOrderOperations>()
                        .GetFulfilmentAsync(
                            fixture.Administrator.UserId,
                            fixture.Administrator.OrganizationId,
                            submitted.OrderNumber,
                            fixture.CancellationToken
                        )
                )
        );
        await fixture.ExecuteSetupAsync(
            "DROP TRIGGER fail_outbox ON sales.outbox_messages; DROP FUNCTION sales.fail_outbox();"
        );
        await fixture.ApproveAsync(submitted);
        await fixture.WaitAsync(submitted.OrderNumber, "reserved");
        Assert.Equal(2m, (await fixture.StockAsync()).ReservedQuantity);
    }

    [Fact]
    public async Task PendingFulfilment_LegacyProcessAndMissingMain_ResumesOnceAfterRestart()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(createMain: false);
        var order = await fixture.ApproveAsync(2m);
        var pending = await fixture.ReadAsync(order.OrderNumber);
        Assert.Equal(OrderFulfilmentStatus.PendingDispatch, pending.Status);
        Assert.Null(pending.StockingLocationId);
        // Retained pre-messaging processes had neither children nor an order-number snapshot.
        // This sets up that legacy shape; all assertions use the product Contracts.
        await fixture.ExecuteSetupAsync(
            $"""
            DELETE FROM sales.fulfilment_lines WHERE process_id = '{pending.ProcessId:D}';
            UPDATE sales.fulfilment_processes SET order_number = 0 WHERE id = '{pending.ProcessId:D}';
            """
        );
        await fixture.RestartAsync(addMain: true);
        var completed = await fixture.WaitAsync(order.OrderNumber, "reserved");
        Assert.Equal(pending.ProcessId, completed.ProcessId);
        Assert.Equal(3, completed.Version);
        Assert.Equal(fixture.Location.Value, completed.StockingLocationId);
        Assert.Equal(2m, (await fixture.StockAsync()).ReservedQuantity);
        await fixture.RestartAsync();
        Assert.Equal(3, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
        Assert.Equal(5, (await fixture.ActivityAsync(order.OrderNumber)).Count);
    }

    [Fact]
    public async Task ReservationOutcomes_OutOfOrderAndRepeatedDelivery_AdvanceEachLineOnlyOnce()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var order = await fixture.ApproveAsync(2m, 3m);
        ReserveStockV1[] commands =
        [
            await fixture.ReadCommandAsync(),
            await fixture.ReadCommandAsync(),
        ];
        var ordered = commands.OrderBy(command => command.LineNumber).ToArray();
        var first = ordered[0];
        var second = ordered[1];
        var last = Reserved(second);
        await fixture.PublishAsync(last);
        await fixture.WaitDeliveryAsync(last.MessageId);
        var intermediate = await fixture.ReadAsync(order.OrderNumber);
        Assert.Equal(OrderFulfilmentStatus.AwaitingReservations, intermediate.Status);
        Assert.Equal(
            1,
            intermediate.Lines.Count(line => line.Status == OrderFulfilmentLineStatus.Reserved)
        );
        await fixture.PublishAsync(last);
        await fixture.WaitDeliveryAsync(last.MessageId, 2);
        var replay = last with
        {
            MessageId = Guid.CreateVersion7(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await fixture.PublishAsync(replay);
        await fixture.WaitDeliveryAsync(replay.MessageId);
        Assert.Equal(3, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Equal(5, (await fixture.ActivityAsync(order.OrderNumber)).Count);
        var remaining = Reserved(first);
        await fixture.PublishAsync(remaining);
        await fixture.WaitDeliveryAsync(remaining.MessageId);
        var completed = await fixture.WaitAsync(order.OrderNumber, "reserved");
        Assert.Equal(4, completed.Version);
        Assert.Equal(6, (await fixture.ActivityAsync(order.OrderNumber)).Count);
    }

    private static StockReservationOutcomeV1 Reserved(ReserveStockV1 command) =>
        new(
            Guid.CreateVersion7(),
            command.MessageId,
            command.OrganizationId,
            command.OperationId,
            command.ProcessId,
            command.OrderNumber,
            command.LineNumber,
            StockReservationOutcome.Reserved,
            Guid.CreateVersion7(),
            command.Quantity,
            8m,
            command.BaseUnitCode,
            null,
            DateTimeOffset.UtcNow
        );

    [Theory]
    [InlineData("tenant")]
    [InlineData("process")]
    [InlineData("order")]
    [InlineData("line")]
    [InlineData("operation")]
    [InlineData("causation")]
    [InlineData("quantity")]
    [InlineData("unit")]
    public async Task ReservationOutcome_ForeignOrStaleCorrelation_DoesNotAdvanceProcess(
        string changed
    )
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var order = await fixture.ApproveAsync(2m);
        var command = await fixture.ReadCommandAsync();
        var valid = Reserved(command);
        var invalid = changed switch
        {
            "tenant" => valid with { OrganizationId = Guid.CreateVersion7() },
            "process" => valid with { ProcessId = Guid.CreateVersion7() },
            "order" => valid with { OrderNumber = command.OrderNumber + 1 },
            "line" => valid with { LineNumber = command.LineNumber + 1 },
            "operation" => valid with { OperationId = Guid.CreateVersion7() },
            "causation" => valid with { CausationId = Guid.CreateVersion7() },
            "quantity" => valid with { RequestedQuantity = 3m },
            "unit" => valid with { BaseUnitCode = "KG" },
            _ => throw new ArgumentOutOfRangeException(nameof(changed)),
        };
        await fixture.PublishAsync(invalid);
        await fixture.WaitDeliveryAsync(invalid.MessageId);
        var unchanged = await fixture.ReadAsync(order.OrderNumber);
        Assert.Equal(2, unchanged.Version);
        Assert.Equal(OrderFulfilmentStatus.AwaitingReservations, unchanged.Status);
        Assert.Equal(4, (await fixture.ActivityAsync(order.OrderNumber)).Count);
        await fixture.PublishAsync(valid with { MessageId = Guid.CreateVersion7() });
        await fixture.WaitAsync(order.OrderNumber, "reserved");
    }

    [Theory]
    [InlineData("inbox_receipts")]
    [InlineData("order_activity")]
    [InlineData("audit_entries")]
    public async Task ReservationOutcome_AtomicParticipantFails_RollsBackAndRedrivesOnce(
        string table
    )
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var order = await fixture.ApproveAsync(2m);
        var outcome = Reserved(await fixture.ReadCommandAsync());
        await fixture.ExecuteSetupAsync(
            $"""
            CREATE FUNCTION sales.fail_outcome() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test outcome fault'; END $$;
            CREATE TRIGGER fail_outcome BEFORE INSERT ON sales.{table}
            FOR EACH ROW EXECUTE FUNCTION sales.fail_outcome();
            """
        );
        await fixture.PublishAsync(outcome);
        Assert.Equal(outcome, await fixture.ReadErrorAsync());
        var rolledBack = await fixture.ReadAsync(order.OrderNumber);
        Assert.Equal(2, rolledBack.Version);
        Assert.Equal(
            OrderFulfilmentLineStatus.PendingReservation,
            Assert.Single(rolledBack.Lines).Status
        );
        Assert.Equal(4, (await fixture.ActivityAsync(order.OrderNumber)).Count);
        await fixture.ExecuteSetupAsync(
            $"DROP TRIGGER fail_outcome ON sales.{table}; DROP FUNCTION sales.fail_outcome();"
        );
        await fixture.PublishAsync(outcome);
        await fixture.WaitDeliveryAsync(outcome.MessageId);
        await fixture.PublishAsync(outcome);
        await fixture.WaitDeliveryAsync(outcome.MessageId, 2);
        Assert.Equal(3, (await fixture.WaitAsync(order.OrderNumber, "reserved")).Version);
        Assert.Equal(5, (await fixture.ActivityAsync(order.OrderNumber)).Count);
    }

    [Fact]
    public async Task ReservationOutcomes_CompetingUpdatesAndLostSettlement_RetainBothLinesOnce()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var order = await fixture.ApproveAsync(2m, 3m);
        var first = Reserved(await fixture.ReadCommandAsync());
        var second = Reserved(await fixture.ReadCommandAsync());
        fixture.FailSettlementOnce(first.MessageId);
        await using (var barrier = await fixture.HoldProcessUpdatesAsync())
        {
            await Task.WhenAll(fixture.PublishAsync(first), fixture.PublishAsync(second));
            await fixture.WaitForCompetingUpdatesAsync();
            // Closing this non-pooled connection releases the test's session lock.
        }
        await fixture.WaitDeliveryAsync(first.MessageId, 2);
        await fixture.WaitDeliveryAsync(second.MessageId);
        var process = await fixture.WaitAsync(order.OrderNumber, "reserved");
        Assert.Equal(4, process.Version);
        Assert.All(
            process.Lines,
            line => Assert.Equal(OrderFulfilmentLineStatus.Reserved, line.Status)
        );
        Assert.Equal(6, (await fixture.ActivityAsync(order.OrderNumber)).Count);
    }

    [Fact]
    public async Task ReservationOutcome_ConflictingIdentity_GoesToSalesErrorQueueWithoutOverwritingDecision()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var order = await fixture.ApproveAsync(2m);
        var accepted = Reserved(await fixture.ReadCommandAsync());
        await fixture.PublishAsync(accepted);
        await fixture.WaitDeliveryAsync(accepted.MessageId);
        var conflictingDecision = accepted with
        {
            MessageId = Guid.CreateVersion7(),
            ReservationId = Guid.CreateVersion7(),
        };
        await fixture.PublishAsync(conflictingDecision);
        Assert.Equal(conflictingDecision, await fixture.ReadErrorAsync());
        var conflictingDelivery = accepted with { AvailableQuantity = 7m };
        await fixture.PublishAsync(conflictingDelivery);
        Assert.Equal(conflictingDelivery, await fixture.ReadErrorAsync());
        var unchanged = await fixture.ReadAsync(order.OrderNumber);
        Assert.Equal(3, unchanged.Version);
        Assert.Equal(accepted.ReservationId, Assert.Single(unchanged.Lines).ReservationId);
        Assert.Equal(5, (await fixture.ActivityAsync(order.OrderNumber)).Count);
        var replay = accepted with { MessageId = Guid.CreateVersion7() };
        await fixture.PublishAsync(replay);
        await fixture.WaitDeliveryAsync(replay.MessageId);
        Assert.Equal(3, (await fixture.ReadAsync(order.OrderNumber)).Version);
    }

    [Fact]
    public async Task ReservationOutcome_RejectedLine_DoesNotDiscardLaterSuccessfulLine()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var order = await fixture.ApproveAsync(2m, 3m);
        var rejected = Reserved(await fixture.ReadCommandAsync()) with
        {
            Outcome = StockReservationOutcome.Rejected,
            ReservationId = null,
            ReasonCode = "location-inactive",
            AvailableQuantity = 0m,
        };
        var reserved = Reserved(await fixture.ReadCommandAsync());
        await fixture.PublishAsync(rejected);
        await fixture.WaitDeliveryAsync(rejected.MessageId);
        Assert.Equal(
            OrderFulfilmentStatus.AttentionRequired,
            (await fixture.ReadAsync(order.OrderNumber)).Status
        );
        await fixture.PublishAsync(reserved);
        await fixture.WaitDeliveryAsync(reserved.MessageId);
        var process = await fixture.ReadAsync(order.OrderNumber);
        Assert.Equal(OrderFulfilmentStatus.AttentionRequired, process.Status);
        Assert.Equal(4, process.Version);
        Assert.Equal(
            1,
            process.Lines.Count(line => line.Status == OrderFulfilmentLineStatus.Rejected)
        );
        Assert.Equal(
            1,
            process.Lines.Count(line => line.Status == OrderFulfilmentLineStatus.Reserved)
        );
        Assert.Equal(6, (await fixture.ActivityAsync(order.OrderNumber)).Count);
    }
}
