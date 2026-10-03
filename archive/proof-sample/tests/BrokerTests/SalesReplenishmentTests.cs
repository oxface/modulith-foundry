using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class SalesReplenishmentTests
{
    [Theory]
    [InlineData("audit_entries", "action", "order-fulfilment.replenishment-outcome-recorded")]
    [InlineData("order_activity", "kind", "replenishment-created")]
    public async Task ReplenishmentCreated_AtomicParticipantFails_RollsBackAndRedrivesOnce(
        string table,
        string discriminator,
        string value
    )
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(enablePurchasing: true);
        await fixture.StartReplenishmentObserverAsync();
        await fixture.ExecuteSetupAsync(
            $"""
            CREATE FUNCTION sales.fail_replenishment() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.{discriminator} = '{value}' THEN
                    RAISE EXCEPTION 'test replenishment participant fault';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER fail_replenishment BEFORE INSERT ON sales.{table}
            FOR EACH ROW EXECUTE FUNCTION sales.fail_replenishment();
            """
        );
        var order = await fixture.ApproveAsync(100m);
        var created = await fixture.ReadCreatedAsync();
        Assert.Equal(created.MessageId, (await fixture.ReadReplenishmentErrorAsync()).MessageId);
        var rolledBack = await fixture.ReadAsync(order.OrderNumber);
        var line = Assert.Single(rolledBack.Lines);
        Assert.Equal(OrderFulfilmentStatus.AwaitingReplenishment, rolledBack.Status);
        Assert.Equal(90m, line.ReplenishmentQuantity);
        Assert.Null(line.ReplenishmentRequirementId);
        Assert.DoesNotContain(
            await fixture.ActivityAsync(order.OrderNumber),
            entry => entry.Kind == SalesOrderActivityKind.ReplenishmentCreated
        );
        Assert.Single(await fixture.RequirementsAsync());
        await fixture.ExecuteSetupAsync(
            $"DROP TRIGGER fail_replenishment ON sales.{table}; DROP FUNCTION sales.fail_replenishment();"
        );
        await fixture.PublishAsync(created);
        var completed = await fixture.WaitForRequirementAsync(order.OrderNumber);
        await fixture.PublishAsync(created);
        await fixture.WaitDeliveryAsync(created.MessageId, 2);
        Assert.Equal(rolledBack.Version + 1, completed.Version);
        Assert.Equal(completed.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Single(
            await fixture.ActivityAsync(order.OrderNumber),
            entry => entry.Kind == SalesOrderActivityKind.ReplenishmentCreated
        );
        Assert.Single(await fixture.RequirementsAsync());
    }

    [Fact]
    public async Task ReplenishmentCreated_RepeatedDeliveryAndLostSettlement_AdvanceOnce()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(enablePurchasing: true);
        await fixture.StartReplenishmentObserverAsync();
        var order = await fixture.ApproveAsync(100m);
        var created = await fixture.ReadCreatedAsync();
        var completed = await fixture.WaitForRequirementAsync(order.OrderNumber);
        var activityCount = (await fixture.ActivityAsync(order.OrderNumber)).Count;
        var replay = created with
        {
            MessageId = Guid.CreateVersion7(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        fixture.FailSettlementOnce(replay.MessageId);
        await fixture.PublishAsync(replay);
        await fixture.WaitDeliveryAsync(replay.MessageId, 2);
        await fixture.PublishAsync(created);
        await fixture.WaitDeliveryAsync(created.MessageId, 2);
        Assert.Equal(completed.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Equal(activityCount, (await fixture.ActivityAsync(order.OrderNumber)).Count);
        Assert.Single(await fixture.RequirementsAsync());
    }

    [Fact]
    public async Task ReplenishmentCreated_ConflictingOutcome_GoesToSalesErrorQueueWithoutOverwrite()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(enablePurchasing: true);
        await fixture.StartReplenishmentObserverAsync();
        var order = await fixture.ApproveAsync(100m);
        var created = await fixture.ReadCreatedAsync();
        var completed = await fixture.WaitForRequirementAsync(order.OrderNumber);
        var conflicting = created with
        {
            MessageId = Guid.CreateVersion7(),
            RequirementId = Guid.CreateVersion7(),
        };
        await fixture.PublishAsync(conflicting);
        Assert.Equal(
            conflicting.MessageId,
            (await fixture.ReadReplenishmentErrorAsync()).MessageId
        );
        var conflictingIdentity = created with
        {
            RequirementNumber = created.RequirementNumber + 1,
        };
        await fixture.PublishAsync(conflictingIdentity);
        Assert.Equal(created.MessageId, (await fixture.ReadReplenishmentErrorAsync()).MessageId);
        Assert.Equal(completed.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Equal(
            created.RequirementId,
            Assert
                .Single((await fixture.ReadAsync(order.OrderNumber)).Lines)
                .ReplenishmentRequirementId
        );
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("stock-item")]
    [InlineData("quantity")]
    [InlineData("causation")]
    public async Task ReplenishmentCreated_ForeignCorrelation_DoesNotAdvanceProcess(string changed)
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(enablePurchasing: true);
        await fixture.StartReplenishmentObserverAsync();
        var order = await fixture.ApproveAsync(100m);
        var created = await fixture.ReadCreatedAsync();
        var completed = await fixture.WaitForRequirementAsync(order.OrderNumber);
        var invalid = changed switch
        {
            "tenant" => created with { OrganizationId = Guid.CreateVersion7() },
            "stock-item" => created with { StockItemId = Guid.CreateVersion7() },
            "quantity" => created with { Quantity = 91m },
            "causation" => created with { CausationId = Guid.CreateVersion7() },
            _ => throw new ArgumentOutOfRangeException(nameof(changed)),
        };
        invalid = invalid with { MessageId = Guid.CreateVersion7() };
        await fixture.PublishAsync(invalid);
        await fixture.WaitDeliveryAsync(invalid.MessageId);
        Assert.Equal(completed.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Equal(
            created.RequirementId,
            Assert
                .Single((await fixture.ReadAsync(order.OrderNumber)).Lines)
                .ReplenishmentRequirementId
        );
        Assert.Single(await fixture.RequirementsAsync());
    }

    [Fact]
    public async Task Replenishment_LegacyShortageWithoutCommand_QueuesOnceAfterRestart()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        await fixture.ExecuteSetupAsync(
            """
            CREATE FUNCTION sales.hold_replenishment_dispatch() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.message_type = 'purchasing.create-replenishment-requirement.v1' THEN
                    RAISE EXCEPTION 'test stops legacy command publication';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER hold_replenishment_dispatch BEFORE UPDATE ON sales.outbox_messages
            FOR EACH ROW EXECUTE FUNCTION sales.hold_replenishment_dispatch();
            """
        );
        var order = await fixture.ApproveAsync(100m);
        var shortage = await fixture.WaitAsync(order.OrderNumber, "awaiting-replenishment");
        await fixture.StopAsync();
        // Arrange the retained pre-5.4 shape without sending a command before cutover.
        await fixture.ExecuteSetupAsync(
            $"""
            DELETE FROM sales.outbox_messages WHERE message_type = 'purchasing.create-replenishment-requirement.v1';
            UPDATE sales.fulfilment_lines SET replenishment_command_message_id = NULL, replenishment_quantity = NULL
            WHERE process_id = '{shortage.ProcessId:D}';
            DROP TRIGGER hold_replenishment_dispatch ON sales.outbox_messages;
            DROP FUNCTION sales.hold_replenishment_dispatch();
            """
        );
        await fixture.RestartAsync(enablePurchasing: true);
        var completed = await fixture.WaitForRequirementAsync(order.OrderNumber);
        Assert.Equal(shortage.ProcessId, completed.ProcessId);
        Assert.Single(await fixture.RequirementsAsync());
        await fixture.RestartAsync();
        Assert.Equal(completed.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Single(await fixture.RequirementsAsync());
    }

    [Fact]
    public async Task Replenishment_InactiveReference_RecordsRejectionAndRequiresAttention()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(
            controlledOutcomes: true,
            enablePurchasing: true
        );
        var order = await fixture.ApproveAsync(100m);
        var command = await fixture.ReadCommandAsync();
        await fixture.DeactivateItemAsync();
        await fixture.PublishAsync(
            new StockReservationOutcomeV1(
                Guid.CreateVersion7(),
                command.MessageId,
                command.OrganizationId,
                command.OperationId,
                command.ProcessId,
                command.OrderNumber,
                command.LineNumber,
                StockReservationOutcome.Shortage,
                null,
                command.Quantity,
                10m,
                command.BaseUnitCode,
                "insufficient-stock",
                DateTimeOffset.UtcNow
            )
        );
        var process = await fixture.WaitForRequirementAsync(order.OrderNumber, rejected: true);
        var line = Assert.Single(process.Lines);
        Assert.Equal(OrderFulfilmentStatus.AttentionRequired, process.Status);
        Assert.Equal("inactive-stock-item", line.ReplenishmentReasonCode);
        Assert.Null(line.ReplenishmentRequirementId);
        Assert.Empty(await fixture.RequirementsAsync());
    }

    [Fact]
    public async Task Fulfilment_Shortage_CreatesRequirementAndRecordsOutcomeWithoutChangingStock()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(enablePurchasing: true);
        var order = await fixture.ApproveAsync(100m);
        var process = await fixture.WaitForRequirementAsync(order.OrderNumber);
        var line = Assert.Single(process.Lines);
        Assert.Equal(OrderFulfilmentLineStatus.Shortage, line.Status);
        Assert.Equal(OrderFulfilmentStatus.AwaitingReplenishment, process.Status);
        var requirement = Assert
            .IsType<GetReplenishmentRequirementResult.Found>(
                await fixture.RequirementAsync(line.ReplenishmentRequirementNumber!.Value)
            )
            .Requirement;
        Assert.Equal(line.ReplenishmentRequirementId, requirement.RequirementId);
        Assert.Equal(90m, requirement.Quantity);
        Assert.Equal("EA", requirement.BaseUnitCode);
        Assert.Equal("BOLT", requirement.Sku);
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Single(await fixture.RequirementsAsync());
        await fixture.RestartAsync();
        Assert.Equal(process.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Single(await fixture.RequirementsAsync());
    }
}
