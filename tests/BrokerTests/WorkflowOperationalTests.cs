using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class WorkflowOperationalTests
{
    [Fact]
    public async Task Fulfilment_ShutdownBeforeDispatch_RestartPreservesIntentWithoutFailureSignal()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await SalesFulfilmentFixture.StartAsync(createMain: false);
        telemetry.Observe(fixture.MeterFactory);
        var order = await fixture.ApproveAsync(2m);
        var pending = await fixture.ReadAsync(order.OrderNumber);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.sales.fulfilment.unsettled",
            value => value == 1,
            fixture.CancellationToken
        );
        await fixture.StopAsync().WaitAsync(TimeSpan.FromSeconds(20), fixture.CancellationToken);
        Assert.Equal(0, telemetry.Counter("modulith_foundry.sales.fulfilment.dispatch_failures"));
        Assert.Equal(
            0,
            telemetry.Counter("modulith_foundry.sales.fulfilment.observation_failures")
        );
        Assert.Equal(
            OrderFulfilmentStatus.PendingDispatch,
            (await fixture.ReadAsync(order.OrderNumber)).Status
        );
        await fixture.RestartAsync(addMain: true);
        var completed = await fixture.WaitAsync(order.OrderNumber, "reserved");
        Assert.Equal(pending.ProcessId, completed.ProcessId);
        Assert.Equal(2m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
        Assert.Single(
            await fixture.ActivityAsync(order.OrderNumber),
            entry => entry.Kind == SalesOrderActivityKind.StockReserved
        );
    }

    [Fact]
    public async Task Fulfilment_BrokerUnavailable_RetainsApprovedIntentAndRecoversWithoutHostRestart()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await SalesFulfilmentFixture.StartAsync(
            scenarioTimeout: TimeSpan.FromMinutes(3)
        );
        telemetry.Observe(fixture.MeterFactory);
        await fixture.StopBrokerAsync();
        var order = await fixture.ApproveAsync(2m);
        var awaiting = await fixture.ReadAsync(order.OrderNumber);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.sales.outbox.pending",
            value => value == 1,
            fixture.CancellationToken
        );
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.sales.fulfilment.unsettled",
            value => value == 1,
            fixture.CancellationToken
        );
        Assert.Equal(OrderFulfilmentStatus.AwaitingReservations, awaiting.Status);
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        await fixture.StartBrokerAsync();
        // The pinned Rebus transport waits one minute after a failed consumer initialization.
        var completed = await fixture.WaitAsync(
            order.OrderNumber,
            "reserved",
            TimeSpan.FromMinutes(2)
        );
        Assert.Equal(awaiting.ProcessId, completed.ProcessId);
        Assert.Equal(2m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(3, (await fixture.StockAsync()).Version);
        Assert.Single(
            await fixture.ActivityAsync(order.OrderNumber),
            entry => entry.Kind == SalesOrderActivityKind.StockReserved
        );
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.sales.outbox.pending",
            value => value == 0,
            fixture.CancellationToken
        );
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.sales.fulfilment.unsettled",
            value => value == 0,
            fixture.CancellationToken
        );
        Assert.Empty(telemetry.Tags);
    }

    [Fact]
    public async Task FulfilmentObservation_SourceUnavailable_RetainsSampleWhileOutboxObservationContinues()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await SalesFulfilmentFixture.StartAsync(createMain: false);
        telemetry.Observe(fixture.MeterFactory);
        var order = await fixture.ApproveAsync(2m);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.sales.fulfilment.unsettled",
            value => value == 1,
            fixture.CancellationToken
        );
        await fixture.ExecuteSetupAsync(
            "ALTER TABLE sales.fulfilment_processes RENAME TO unavailable_fulfilment_processes;"
        );
        try
        {
            await telemetry.WaitCounterAsync(
                "modulith_foundry.sales.fulfilment.observation_failures",
                1,
                fixture.CancellationToken
            );
            await telemetry.WaitGaugeAsync(
                "modulith_foundry.sales.fulfilment.sample_age",
                value => value >= 7,
                fixture.CancellationToken
            );
            await telemetry.WaitGaugeAsync(
                "modulith_foundry.sales.fulfilment.unsettled",
                value => value == 1,
                fixture.CancellationToken
            );
            await telemetry.WaitGaugeAsync(
                "modulith_foundry.sales.outbox.sample_age",
                value => value < 3,
                fixture.CancellationToken
            );
        }
        finally
        {
            await fixture.ExecuteSetupAsync(
                "ALTER TABLE sales.unavailable_fulfilment_processes RENAME TO fulfilment_processes;"
            );
        }
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.sales.fulfilment.sample_age",
            value => value < 1,
            fixture.CancellationToken
        );
        Assert.Equal(
            OrderFulfilmentStatus.PendingDispatch,
            (await fixture.ReadAsync(order.OrderNumber)).Status
        );
        await fixture.AddMainAsync();
        await fixture.WaitAsync(order.OrderNumber, "reserved");
        Assert.Equal(2m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Empty(telemetry.Tags);
    }

    [Fact]
    public async Task FulfilmentObservation_PendingThenReserved_ReportsAgeAndClearsUnsettledWork()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await SalesFulfilmentFixture.StartAsync(createMain: false);
        telemetry.Observe(fixture.MeterFactory);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.sales.fulfilment.unsettled",
            value => value == 0,
            fixture.CancellationToken
        );
        var order = await fixture.ApproveAsync(2m);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.sales.fulfilment.unsettled",
            value => value == 1,
            fixture.CancellationToken
        );
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.sales.fulfilment.oldest_age",
            value => value >= 1,
            fixture.CancellationToken
        );
        Assert.Equal(
            OrderFulfilmentStatus.PendingDispatch,
            (await fixture.ReadAsync(order.OrderNumber)).Status
        );
        await fixture.AddMainAsync();
        await fixture.WaitAsync(order.OrderNumber, "reserved");
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.sales.fulfilment.unsettled",
            value => value == 0,
            fixture.CancellationToken
        );
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.sales.fulfilment.oldest_age",
            value => value == 0,
            fixture.CancellationToken
        );
        Assert.Equal(2m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Empty(telemetry.Tags);
    }

    [Fact]
    public async Task FulfilmentDispatch_DamagedProcess_ReportsFailureWhileOtherWorkCompletes()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await SalesFulfilmentFixture.StartAsync(
            createMain: false,
            logs: telemetry
        );
        telemetry.Observe(fixture.MeterFactory);
        var damaged = await fixture.ApproveAsync(2m);
        var healthy = await fixture.ApproveAsync(3m);
        var pending = await fixture.ReadAsync(damaged.OrderNumber);
        await fixture.ExecuteSetupAsync(
            $"""
            CREATE FUNCTION sales.reject_damaged_process() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.id = '{pending.ProcessId}' THEN
                    RAISE EXCEPTION 'private-process-data-do-not-export';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER reject_damaged_process BEFORE UPDATE ON sales.fulfilment_processes
                FOR EACH ROW EXECUTE FUNCTION sales.reject_damaged_process();
            """
        );
        await fixture.AddMainAsync();
        await telemetry.WaitCounterAsync(
            "modulith_foundry.sales.fulfilment.dispatch_failures",
            1,
            fixture.CancellationToken
        );
        Assert.Equal(
            OrderFulfilmentStatus.Reserved,
            (await fixture.WaitAsync(healthy.OrderNumber, "reserved")).Status
        );
        Assert.Equal(
            OrderFulfilmentStatus.PendingDispatch,
            (await fixture.ReadAsync(damaged.OrderNumber)).Status
        );
        Assert.Equal(3m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Contains(
            telemetry.Logs,
            log => log.Contains(pending.ProcessId.ToString(), StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            telemetry.Logs,
            log => log.Contains("private-process-data-do-not-export", StringComparison.Ordinal)
        );
        Assert.Empty(telemetry.Tags);
        await fixture.ExecuteSetupAsync(
            "DROP TRIGGER reject_damaged_process ON sales.fulfilment_processes; DROP FUNCTION sales.reject_damaged_process();"
        );
        await fixture.WaitAsync(damaged.OrderNumber, "reserved");
        Assert.Equal(5m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(pending.ProcessId, (await fixture.ReadAsync(damaged.OrderNumber)).ProcessId);
        Assert.Single(
            await fixture.ActivityAsync(damaged.OrderNumber),
            entry => entry.Kind == SalesOrderActivityKind.StockReserved
        );
    }
}
