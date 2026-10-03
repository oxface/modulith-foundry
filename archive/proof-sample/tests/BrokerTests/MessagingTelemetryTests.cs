using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class MessagingTelemetryTests
{
    [Fact]
    public async Task PurchasingInbox_IdenticalReceipt_ReportsSuppressionWithoutAnotherRequirement()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await StockItemBootstrapFixture.StartAsync();
        var item = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        telemetry.Observe(fixture.MeterFactory);
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
        await fixture.SendAsync(command);
        var created = await fixture.ReadCreatedAsync();
        await fixture.WaitForProcessedAsync(command.MessageId);
        await fixture.SendAsync(command);
        await fixture.WaitForProcessedAsync(command.MessageId, 2);
        Assert.Equal(1, telemetry.Counter("modulith_foundry.purchasing.inbox.duplicates"));
        var requirement = Assert.IsType<GetReplenishmentRequirementResult.Found>(
            await fixture.RequirementAsync(created.RequirementNumber)
        );
        Assert.Equal(created.RequirementId, requirement.Requirement.RequirementId);
        Assert.Single(
            Assert
                .IsType<ListReplenishmentRequirementsResult.Listed>(
                    await fixture.RequirementsAsync()
                )
                .Requirements
        );
        Assert.Empty(telemetry.Tags);
    }

    [Fact]
    public async Task SalesInbox_IdenticalReleaseReceipt_ReportsSuppressionWithoutAnotherCompletion()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        telemetry.Observe(fixture.MeterFactory);
        var (order, release) = await fixture.PrepareControlledCancellationAsync();
        var outcome = new StockReservationReleaseOutcomeV1(
            Guid.CreateVersion7(),
            release.MessageId,
            release.OrganizationId,
            release.OperationId,
            release.ProcessId,
            release.OrderNumber,
            release.LineNumber,
            release.ReservationOperationId,
            release.ReservationId,
            StockReservationReleaseOutcome.Released,
            4m,
            "EA",
            null,
            DateTimeOffset.UtcNow
        );
        await fixture.PublishAsync(outcome);
        await fixture.WaitDeliveryAsync(outcome.MessageId);
        var completed = await fixture.WaitAsync(order.OrderNumber, "compensated");
        await fixture.PublishAsync(outcome);
        await fixture.WaitDeliveryAsync(outcome.MessageId, 2);
        Assert.Equal(1, telemetry.Counter("modulith_foundry.sales.inbox.duplicates"));
        Assert.Equal(completed.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Single(
            await fixture.ActivityAsync(order.OrderNumber),
            item => item.Kind == SalesOrderActivityKind.CompensationCompleted
        );
        Assert.Empty(telemetry.Tags);
    }

    [Fact]
    public async Task InventoryOutbox_ExpiredLease_ReportsRetainedWorkUntilExistingRelayRecovers()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await ReservationFixture.StartAsync();
        telemetry.Observe(fixture.MeterFactory);
        await fixture.SendAsync(fixture.Command(4m));
        var original = await fixture.ReadOutcomeAsync();
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.pending",
            value => value == 0,
            fixture.CancellationToken
        );
        await fixture.ExecuteSqlAsync(
            $"""
            UPDATE inventory.outbox_messages SET dispatched_at = NULL,
                available_at = clock_timestamp() + interval '5 minutes',
                lease_token = '{Guid.CreateVersion7()}', lease_until = clock_timestamp() - interval '1 second'
            WHERE message_id = '{original.MessageId}';
            """
        );
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.expired_leases",
            value => value == 1,
            fixture.CancellationToken
        );
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.pending",
            value => value == 1,
            fixture.CancellationToken
        );
        await fixture.ExecuteSqlAsync(
            $"UPDATE inventory.outbox_messages SET available_at = clock_timestamp() WHERE message_id = '{original.MessageId}';"
        );
        Assert.Equal(original.MessageId, (await fixture.ReadOutcomeAsync()).MessageId);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.pending",
            value => value == 0,
            fixture.CancellationToken
        );
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.expired_leases",
            value => value == 0,
            fixture.CancellationToken
        );
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task InventoryInbox_IdenticalReceipt_ReportsSuppressionNotNewSemanticDelivery()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await ReservationFixture.StartAsync();
        telemetry.Observe(fixture.MeterFactory);
        var command = fixture.Command(4m);
        await fixture.SendAsync(command);
        await fixture.ReadOutcomeAsync();
        await fixture.WaitForProcessedAsync(command.MessageId);
        Assert.Equal(0, telemetry.Counter("modulith_foundry.inventory.inbox.duplicates"));
        await fixture.SendAsync(command);
        await fixture.WaitForProcessedAsync(command.MessageId, 2);
        Assert.Equal(1, telemetry.Counter("modulith_foundry.inventory.inbox.duplicates"));
        var repeatedOperation = command with
        {
            MessageId = Guid.CreateVersion7(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await fixture.SendAsync(repeatedOperation);
        await fixture.WaitForProcessedAsync(repeatedOperation.MessageId);
        Assert.Equal(1, telemetry.Counter("modulith_foundry.inventory.inbox.duplicates"));
        Assert.Equal(3, (await fixture.StockAsync()).Version);
        Assert.Empty(telemetry.Tags);
    }

    [Fact]
    public async Task InventoryOutbox_DispatchMarkFails_ReportsFailureWithoutPrivateExceptionText()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await ReservationFixture.StartAsync(logs: telemetry);
        telemetry.Observe(fixture.MeterFactory);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.pending",
            value => value == 0,
            fixture.CancellationToken
        );
        const string privateText = "private-token-and-payload-do-not-export";
        await fixture.ExecuteSqlAsync(
            $"""
            CREATE FUNCTION inventory.reject_test_mark() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.dispatched_at IS NOT NULL THEN RAISE EXCEPTION '{privateText}'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER reject_test_mark BEFORE UPDATE ON inventory.outbox_messages
            FOR EACH ROW EXECUTE FUNCTION inventory.reject_test_mark();
            """
        );
        await fixture.SendAsync(fixture.Command(4m));
        var original = await fixture.ReadOutcomeAsync();
        await telemetry.WaitCounterAsync(
            "modulith_foundry.inventory.outbox.dispatch_failures",
            1,
            fixture.CancellationToken
        );
        Assert.Contains(
            telemetry.Logs,
            value => value.Contains(original.MessageId.ToString(), StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            telemetry.Logs,
            value => value.Contains(privateText, StringComparison.Ordinal)
        );
        Assert.Empty(telemetry.Tags);
        await fixture.ExecuteSqlAsync(
            """
            DROP TRIGGER reject_test_mark ON inventory.outbox_messages;
            DROP FUNCTION inventory.reject_test_mark();
            """
        );
        Assert.Equal(original.MessageId, (await fixture.ReadOutcomeAsync()).MessageId);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.pending",
            value => value == 0,
            fixture.CancellationToken
        );
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task InventoryOutbox_ObservationFails_RetainsBacklogAndReportsStaleness()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await ReservationFixture.StartAsync(logs: telemetry);
        telemetry.Observe(fixture.MeterFactory);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.pending",
            value => value == 0,
            fixture.CancellationToken
        );
        await fixture.ExecuteSqlAsync(
            """
            CREATE FUNCTION inventory.block_test_dispatch() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test relay unavailable'; END $$;
            CREATE TRIGGER block_test_dispatch BEFORE UPDATE ON inventory.outbox_messages
            FOR EACH ROW EXECUTE FUNCTION inventory.block_test_dispatch();
            """
        );
        var command = fixture.Command(4m);
        await fixture.SendAsync(command);
        await fixture.WaitForProcessedAsync(command.MessageId);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.pending",
            value => value == 1,
            fixture.CancellationToken
        );
        await fixture.ExecuteSqlAsync(
            "ALTER TABLE inventory.outbox_messages RENAME TO test_unavailable_outbox;"
        );
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.sample_age",
            value => value >= 7,
            fixture.CancellationToken
        );
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.pending",
            value => value == 1,
            fixture.CancellationToken
        );
        Assert.True(
            telemetry.Counter("modulith_foundry.inventory.outbox.observation_failures") >= 1
        );
        Assert.Contains(
            telemetry.Logs,
            value => value.Contains("last sample is stale", StringComparison.Ordinal)
        );
        await fixture.ExecuteSqlAsync(
            """
            ALTER TABLE inventory.test_unavailable_outbox RENAME TO outbox_messages;
            DROP TRIGGER block_test_dispatch ON inventory.outbox_messages;
            DROP FUNCTION inventory.block_test_dispatch();
            """
        );
        await fixture.ReadOutcomeAsync();
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.pending",
            value => value == 0,
            fixture.CancellationToken
        );
        Assert.Equal(3, (await fixture.StockAsync()).Version);
    }

    [Theory]
    [InlineData("sales")]
    [InlineData("purchasing")]
    public async Task WorkflowOutbox_CommittedBacklog_ReportsAgeAndClearsAfterPublication(
        string module
    )
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await SalesFulfilmentFixture.StartAsync(
            enablePurchasing: true,
            logs: telemetry
        );
        telemetry.Observe(fixture.MeterFactory);
        string prefix = $"modulith_foundry.{module}.outbox";
        await telemetry.WaitGaugeAsync(
            prefix + ".pending",
            value => value == 0,
            fixture.CancellationToken
        );
        await fixture.ExecuteSetupAsync(
            $"""
            CREATE FUNCTION {module}.block_test_dispatch() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test relay unavailable'; END $$;
            CREATE TRIGGER block_test_dispatch BEFORE UPDATE ON {module}.outbox_messages
            FOR EACH ROW EXECUTE FUNCTION {module}.block_test_dispatch();
            """
        );
        var order = await fixture.ApproveAsync(module == "sales" ? 4m : 100m);
        await telemetry.WaitGaugeAsync(
            prefix + ".pending",
            value => value == 1,
            fixture.CancellationToken
        );
        await telemetry.WaitGaugeAsync(
            prefix + ".oldest_age",
            value => value >= 1,
            fixture.CancellationToken
        );
        await fixture.ExecuteSetupAsync(
            $"""
            DROP TRIGGER block_test_dispatch ON {module}.outbox_messages;
            DROP FUNCTION {module}.block_test_dispatch();
            """
        );
        if (module == "sales")
            await fixture.WaitAsync(order.OrderNumber, "reserved");
        else
            await fixture.WaitForRequirementAsync(order.OrderNumber);
        await telemetry.WaitGaugeAsync(
            prefix + ".pending",
            value => value == 0,
            fixture.CancellationToken
        );
        await telemetry.WaitGaugeAsync(
            prefix + ".oldest_age",
            value => value == 0,
            fixture.CancellationToken
        );
        Assert.Empty(telemetry.Tags);
    }

    [Fact]
    public async Task InventoryOutbox_CommittedBacklog_ReportsAgeAndClearsAfterPublication()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await ReservationFixture.StartAsync(logs: telemetry);
        telemetry.Observe(fixture.MeterFactory);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.pending",
            value => value == 0,
            fixture.CancellationToken
        );
        await fixture.ExecuteSqlAsync(
            """
            CREATE FUNCTION inventory.block_test_dispatch() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test relay unavailable'; END $$;
            CREATE TRIGGER block_test_dispatch BEFORE UPDATE ON inventory.outbox_messages
            FOR EACH ROW EXECUTE FUNCTION inventory.block_test_dispatch();
            """
        );
        var command = fixture.Command(4m);
        await fixture.SendAsync(command);
        await fixture.WaitForProcessedAsync(command.MessageId);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.pending",
            value => value == 1,
            fixture.CancellationToken
        );
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.oldest_age",
            value => value >= 1,
            fixture.CancellationToken
        );
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        await fixture.ExecuteSqlAsync(
            """
            DROP TRIGGER block_test_dispatch ON inventory.outbox_messages;
            DROP FUNCTION inventory.block_test_dispatch();
            """
        );
        Assert.Equal(command.OperationId, (await fixture.ReadOutcomeAsync()).OperationId);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.pending",
            value => value == 0,
            fixture.CancellationToken
        );
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.inventory.outbox.oldest_age",
            value => value == 0,
            fixture.CancellationToken
        );
        Assert.Empty(telemetry.Tags);
    }
}
