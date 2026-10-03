using ModulithFoundry.Modules.Purchasing.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class ProjectionOperationalTests
{
    [Fact]
    public async Task Bootstrap_CommitUnavailable_ReportsFailureAndRecoversWithoutHostRestart()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await StockItemBootstrapFixture.StartAsync(logs: telemetry);
        telemetry.Observe(fixture.MeterFactory);
        var item = await fixture.CreateAsync("BOLT");
        await fixture.ExecuteSqlAsync(
            """
            CREATE FUNCTION purchasing.reject_test_bootstrap() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'private-bootstrap-content-do-not-export'; END $$;
            CREATE TRIGGER reject_test_bootstrap BEFORE UPDATE ON purchasing.stock_item_bootstrap
            FOR EACH ROW EXECUTE FUNCTION purchasing.reject_test_bootstrap();
            """
        );
        await fixture.StartEndpointsAsync();
        try
        {
            await telemetry.WaitCounterAsync(
                "modulith_foundry.purchasing.stock_item_projection.bootstrap_failures",
                1,
                fixture.CancellationToken
            );
            await telemetry.WaitGaugeAsync(
                "modulith_foundry.purchasing.stock_item_projection.ready",
                value => value == 0,
                fixture.CancellationToken
            );
            Assert.False((await fixture.StatusAsync()).IsReady);
            Assert.Null(await fixture.GetAsync(item));
            Assert.Equal(
                0,
                telemetry.Counter(
                    "modulith_foundry.purchasing.stock_item_projection.observation_failures"
                )
            );
            Assert.Contains(
                telemetry.Logs,
                message => message.Contains("bootstrap failed", StringComparison.Ordinal)
            );
            Assert.DoesNotContain(
                telemetry.Logs,
                message =>
                    message.Contains(
                        "private-bootstrap-content-do-not-export",
                        StringComparison.Ordinal
                    )
            );
        }
        finally
        {
            await fixture.ExecuteSqlAsync(
                """
                DROP TRIGGER reject_test_bootstrap ON purchasing.stock_item_bootstrap;
                DROP FUNCTION purchasing.reject_test_bootstrap();
                """
            );
        }
        await fixture.WaitForReadyAsync();
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.purchasing.stock_item_projection.ready",
            value => value == 1,
            fixture.CancellationToken
        );
        Assert.IsType<StockItemProjectionView>(await fixture.GetAsync(item));
        Assert.Empty(telemetry.Tags);
    }

    [Fact]
    public async Task ProjectionObservation_SourceUnavailable_RetainsReadinessWhileOutboxRemainsFresh()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await StockItemBootstrapFixture.StartAsync();
        var item = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        telemetry.Observe(fixture.MeterFactory);
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.purchasing.stock_item_projection.ready",
            value => value == 1,
            fixture.CancellationToken
        );
        await fixture.ExecuteSqlAsync(
            "ALTER TABLE purchasing.stock_item_bootstrap RENAME TO test_unavailable_bootstrap;"
        );
        try
        {
            await telemetry.WaitCounterAsync(
                "modulith_foundry.purchasing.stock_item_projection.observation_failures",
                1,
                fixture.CancellationToken
            );
            await telemetry.WaitGaugeAsync(
                "modulith_foundry.purchasing.stock_item_projection.sample_age",
                value => value >= 7,
                fixture.CancellationToken
            );
            await telemetry.WaitGaugeAsync(
                "modulith_foundry.purchasing.stock_item_projection.ready",
                value => value == 1,
                fixture.CancellationToken
            );
            await telemetry.WaitGaugeAsync(
                "modulith_foundry.purchasing.outbox.sample_age",
                value => value < 3,
                fixture.CancellationToken
            );
        }
        finally
        {
            await fixture.ExecuteSqlAsync(
                "ALTER TABLE purchasing.test_unavailable_bootstrap RENAME TO stock_item_bootstrap;"
            );
        }
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.purchasing.stock_item_projection.sample_age",
            value => value < 1,
            fixture.CancellationToken
        );
        Assert.True((await fixture.StatusAsync()).IsReady);
        Assert.IsType<StockItemProjectionView>(await fixture.GetAsync(item));
        Assert.Empty(telemetry.Tags);
    }

    [Fact]
    public async Task ProjectionObservation_BootstrapCompletes_ReportsReadyWithoutClaimingTailFreshness()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await StockItemBootstrapFixture.StartAsync();
        telemetry.Observe(fixture.MeterFactory);
        var item = await fixture.CreateAsync("BOLT");
        fixture.PauseSnapshotAfterWatermark();
        await fixture.StartEndpointsAsync();
        await fixture.WaitForSnapshotWatermarkAsync();
        try
        {
            await telemetry.WaitGaugeAsync(
                "modulith_foundry.purchasing.stock_item_projection.ready",
                value => value == 0,
                fixture.CancellationToken
            );
            Assert.False((await fixture.StatusAsync()).IsReady);
        }
        finally
        {
            fixture.ReleaseSnapshot();
        }
        await fixture.WaitForReadyAsync();
        await telemetry.WaitGaugeAsync(
            "modulith_foundry.purchasing.stock_item_projection.ready",
            value => value == 1,
            fixture.CancellationToken
        );
        Assert.IsType<StockItemProjectionView>(await fixture.GetAsync(item));
        Assert.Empty(telemetry.Tags);
    }
}
