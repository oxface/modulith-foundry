using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class SalesCompensationOutcomeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleaseOutcome_RepeatedDeliveryAndLostSettlement_CompletesOnlyOnce(
        bool alreadyReleased
    )
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var (order, release) = await fixture.PrepareControlledCancellationAsync();
        var outcome = Released(release) with
        {
            Outcome = alreadyReleased
                ? StockReservationReleaseOutcome.AlreadyReleased
                : StockReservationReleaseOutcome.Released,
        };
        fixture.FailSettlementOnce(outcome.MessageId);
        await fixture.PublishAsync(outcome);
        await fixture.WaitDeliveryAsync(outcome.MessageId, 2);
        var completed = await fixture.WaitAsync(order.OrderNumber, "compensated");
        var activities = await fixture.ActivityAsync(order.OrderNumber);
        await fixture.PublishAsync(outcome);
        await fixture.WaitDeliveryAsync(outcome.MessageId, 3);
        var replay = outcome with
        {
            MessageId = Guid.CreateVersion7(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await fixture.PublishAsync(replay);
        await fixture.WaitDeliveryAsync(replay.MessageId);
        Assert.Equal(completed.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Equal(activities.Count, (await fixture.ActivityAsync(order.OrderNumber)).Count);
        Assert.Single(
            activities,
            item => item.Kind == SalesOrderActivityKind.CompensationCompleted
        );
    }

    [Fact]
    public async Task ReleaseOutcome_RejectedOrConflictingDecision_RequiresAttentionAndPreservesFirstOutcome()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var (order, release) = await fixture.PrepareControlledCancellationAsync();
        var rejected = Released(release) with
        {
            Outcome = StockReservationReleaseOutcome.Rejected,
            ReservationQuantity = null,
            BaseUnitCode = null,
            ReasonCode = "reservation-unavailable",
        };
        await fixture.PublishAsync(rejected);
        await fixture.WaitDeliveryAsync(rejected.MessageId);
        var before = await fixture.WaitAsync(order.OrderNumber, "attention-required");
        Assert.Equal(
            OrderFulfilmentReleaseStatus.Rejected,
            Assert.Single(before.Lines).ReleaseStatus
        );
        Assert.DoesNotContain(
            await fixture.ActivityAsync(order.OrderNumber),
            item => item.Kind == SalesOrderActivityKind.CompensationCompleted
        );
        var conflicting = Released(release);
        await fixture.PublishAsync(conflicting);
        Assert.Equal(conflicting, await fixture.ReadReleaseErrorAsync());
        Assert.Equal(before.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("causation")]
    [InlineData("operation")]
    [InlineData("quantity")]
    public async Task ReleaseOutcome_ForeignCorrelation_DoesNotCompleteCompensation(string mismatch)
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var (order, release) = await fixture.PrepareControlledCancellationAsync();
        var before = await fixture.ReadAsync(order.OrderNumber);
        var outcome = Released(release);
        var foreign = mismatch switch
        {
            "tenant" => outcome with { OrganizationId = Guid.CreateVersion7() },
            "causation" => outcome with { CausationId = Guid.CreateVersion7() },
            "operation" => outcome with { ReservationOperationId = Guid.CreateVersion7() },
            "quantity" => outcome with { ReservationQuantity = 3m },
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
        };
        foreign = foreign with { MessageId = Guid.CreateVersion7() };
        await fixture.PublishAsync(foreign);
        await fixture.WaitDeliveryAsync(foreign.MessageId);
        Assert.Equal(before.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Equal(
            OrderFulfilmentStatus.CompensationPending,
            (await fixture.ReadAsync(order.OrderNumber)).Status
        );
        await fixture.PublishAsync(outcome);
        await fixture.WaitAsync(order.OrderNumber, "compensated");
    }

    [Theory]
    [InlineData("inbox_receipts")]
    [InlineData("fulfilment_processes")]
    [InlineData("fulfilment_lines")]
    [InlineData("order_activity")]
    [InlineData("audit_entries")]
    public async Task ReleaseOutcome_AtomicParticipantFails_RollsBackAndRedrivesOnce(string table)
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var (order, release) = await fixture.PrepareControlledCancellationAsync();
        var before = await fixture.ReadAsync(order.OrderNumber);
        await fixture.ExecuteSetupAsync(
            $$"""
            CREATE FUNCTION sales.reject_release_outcome() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test release outcome atomicity fault'; END $$;
            CREATE TRIGGER reject_release_outcome BEFORE INSERT OR UPDATE ON sales.{{table}}
            FOR EACH ROW EXECUTE FUNCTION sales.reject_release_outcome();
            """
        );
        var outcome = Released(release);
        await fixture.PublishAsync(outcome);
        Assert.Equal(outcome, await fixture.ReadReleaseErrorAsync());
        Assert.Equal(before.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Equal(
            OrderFulfilmentStatus.CompensationPending,
            (await fixture.ReadAsync(order.OrderNumber)).Status
        );
        await fixture.ExecuteSetupAsync(
            $"DROP TRIGGER reject_release_outcome ON sales.{table}; DROP FUNCTION sales.reject_release_outcome();"
        );
        await fixture.PublishAsync(outcome);
        await fixture.WaitAsync(order.OrderNumber, "compensated");
        Assert.Single(
            await fixture.ActivityAsync(order.OrderNumber),
            item => item.Kind == SalesOrderActivityKind.CompensationCompleted
        );
    }

    internal static StockReservationReleaseOutcomeV1 Released(ReleaseReservationV1 command) =>
        new(
            Guid.CreateVersion7(),
            command.MessageId,
            command.OrganizationId,
            command.OperationId,
            command.ProcessId,
            command.OrderNumber,
            command.LineNumber,
            command.ReservationOperationId,
            command.ReservationId,
            StockReservationReleaseOutcome.Released,
            4m,
            "EA",
            null,
            DateTimeOffset.UtcNow
        );
}
